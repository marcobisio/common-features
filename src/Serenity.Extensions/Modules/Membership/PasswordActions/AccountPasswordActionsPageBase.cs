using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace Serenity.Extensions;

public abstract class AccountPasswordActionsPageBase<TUserRow> : MembershipPageBase<TUserRow>
    where TUserRow : class, IRow, IIdRow, IEmailRow, IPasswordRow, new()
{
    protected string ModuleFolder => "~/Serenity.Extensions/esm/Modules/Membership/PasswordActions/";
    protected string ModulePath(string key) => ModuleFolder + key +  "Page.js";

    [HttpGet, PageAuthorize]
    public virtual ActionResult ChangePassword(
        [FromServices] IUserRetrieveService userRetrieveService)
    {
        var userDefinition = User.GetUserDefinition<IUserDefinition>(userRetrieveService);
        if (userDefinition is IHasPassword hasPassword &&
            !hasPassword.HasPassword)
        {
            return SetPassword();
        }

        return this.PanelPage(new()
        {
            Module = ModulePath(nameof(ChangePassword)),
            PageTitle = ExtensionsTexts.Forms.Membership.ChangePassword.FormTitle
        });
    }

    [HttpGet, PageAuthorize]
    public ActionResult SetPassword()
    {
        return this.PanelPage(new()
        {
            Module = ModulePath("SetPassword"),
            PageTitle = ExtensionsTexts.Forms.Membership.ChangePassword.SetPassword
        });
    }    

    [HttpPost, ServiceAuthorize]
    public virtual ActionResult SendResetPassword(
        [FromServices] IUserRetrieveService userRetrieveService,
        [FromServices] IEmailSender emailSender,
        [FromServices] ISiteAbsoluteUrl siteAbsoluteUrl,
        [FromServices] ITextLocalizer localizer)
    {
        var userDefinition = User.GetUserDefinition<IUserDefinition>(userRetrieveService) ?? 
            throw new ValidationError("Couldn't find user definition.");

#if (IsPublicDemo)
        return this.UseConnection(GetConnectionKey(), connection =>
        {
            var user = connection.TryFirst<TUserRow>(new TUserRow().Fields.IdField == Convert.ToInt32(userDefinition.Id));
            if (user is null)
                throw new ValidationError("Couldn't find user.");

            return new SendResetPasswordResponse()
            {
                DemoLink = "/Account/ResetPassword?t=" + Uri.EscapeDataString(GenerateResetPasswordToken(user))
            };
        });
#else

        return ForgotPassword(new()
        {
            Email = userDefinition.Email
        }, emailSender, siteAbsoluteUrl, localizer);
#endif
    }

    [HttpPost, JsonRequest, ServiceAuthorize]
    public virtual Result<ServiceResponse> ChangePassword(ChangePasswordRequest request,
        [FromServices] ITwoLevelCache cache,
        [FromServices] IUserPasswordValidator passwordValidator,
        [FromServices] IUserRetrieveService userRetrieveService,
        [FromServices] IOptions<MembershipSettings> membershipOptions,
        [FromServices] IOptions<EnvironmentSettings> environmentOptions,
        [FromServices] ITextLocalizer localizer)
    {
        return this.InTransaction("Default", uow =>
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrEmpty(request.OldPassword))
                throw new ArgumentNullException(nameof(request.OldPassword));

            if (passwordValidator is null)
                throw new ArgumentNullException(nameof(passwordValidator));

            var username = User.Identity?.Name;

            var userDefinition = User.GetUserDefinition<IUserDefinition>(userRetrieveService);

            if (userDefinition is not IHasPassword hasPassword ||
                hasPassword.HasPassword)
            {
                if (passwordValidator.Validate(ref username, request.OldPassword) != PasswordValidationResult.Valid)
                    throw new ValidationError("CurrentPasswordMismatch", localizer.Get("Validation.CurrentPasswordMismatch"));

                if (request.ConfirmPassword != request.NewPassword)
                    throw new ValidationError("PasswordConfirmMismatch", localizer.Get("Validation.PasswordConfirm"));
            }

            request.NewPassword = ValidateNewPassword(request.NewPassword, membershipOptions.Value, localizer);

            var salt = GenerateSalt(membershipOptions.Value);
            var hash = CalculateHash(request.NewPassword, salt);
            var userId = User.GetIdentifier();
#if (IsPublicDemo)
            if (userId?.ToString() == "1")
                throw new ValidationError("Sorry, but no changes are allowed in public demo on ADMIN user!");
#endif

            var row = new TUserRow();
            row.IdField.AsObject(row, row.IdField.ConvertValue(userId, CultureInfo.InvariantCulture));
            if (row is IUpdateLogRow updateLogRow)
                updateLogRow.UpdateDateField[row] = DateTime.UtcNow;
            row.PasswordHashField[row] = hash;
            row.PasswordSaltField[row] = salt;
            uow.Connection.UpdateById(row);

            cache.InvalidateOnCommit(uow, row.Fields);

            return new ServiceResponse();
        });
    }

    [HttpGet]
    public virtual ActionResult ForgotPassword()
    {
        return this.PanelPage(GetForgotPasswordPageModel());
    }

    protected virtual ModulePageModel GetForgotPasswordPageModel()
    {
        return new ModulePageModel()
        {
            Module = ModulePath(nameof(ForgotPassword)),
            PageTitle = ExtensionsTexts.Forms.Membership.ForgotPassword.FormTitle,
            Layout = "_LayoutNoNavigation"
        };
    }

    [HttpPost, JsonRequest]
    public virtual Result<ServiceResponse> ForgotPassword(ForgotPasswordRequest request,
        [FromServices] IEmailSender emailSender,
        [FromServices] ISiteAbsoluteUrl siteAbsoluteUrl,
        [FromServices] ITextLocalizer localizer)
    {
        return this.UseConnection(GetConnectionKey(), connection =>
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrEmpty(request.Email))
                throw new ArgumentNullException(nameof(request.Email));

            var fieldsRow = new TUserRow();

            var user = connection.TryFirst<TUserRow>(fieldsRow.EmailField == request.Email);
            if (user == null)
                return new ServiceResponse();

            var token = GenerateResetPasswordToken(user);
            var externalUrl = siteAbsoluteUrl.GetExternalUrl();
            var resetLink = UriHelper.Combine(externalUrl, "Account/ResetPassword?t=");
            resetLink += Uri.EscapeDataString(token);

            var displayNameField = (fieldsRow as IDisplayNameRow).DisplayNameField ??
                fieldsRow.NameField as StringField ??
                fieldsRow.EmailField;

            var emailModel = new ResetPasswordEmailModel
            {
                DisplayName = displayNameField[user],
                ResetLink = resetLink
            };

            var emailSubject = ExtensionsTexts.Forms.Membership.ResetPassword.EmailSubject.ToString(localizer);
            var emailBody = TemplateHelper.RenderViewToString(HttpContext.RequestServices,
                MVC.Views.Membership.PasswordActions.ResetPasswordEmail, emailModel);

            if (emailSender is null)
                throw new ArgumentNullException(nameof(emailSender));

            emailSender.Send(subject: emailSubject, body: emailBody, mailTo: user.EmailField[user]);

            return new ServiceResponse();
        });
    }

    protected virtual string GenerateResetPasswordToken(TUserRow user)
    {
        byte[] bytes;
        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(DateTime.UtcNow.AddHours(3).ToBinary());
            bw.Write(Convert.ToString(user.IdField.AsObject(user), CultureInfo.InvariantCulture));
            bw.Write(GetNonceFor(user));
            bw.Flush();
            bytes = ms.ToArray();
        }

        return Convert.ToBase64String(HttpContext.RequestServices
            .GetDataProtector("ResetPassword").Protect(bytes));
    }

    [HttpGet]
    public virtual IActionResult ResetPassword(string t,
        [FromServices] ISqlConnections sqlConnections,
        [FromServices] ITextLocalizer localizer,
        [FromServices] IOptions<MembershipSettings> options)
    {
        object userId;
        int nonce;
        try
        {
            var bytes = HttpContext.RequestServices
                .GetDataProtector("ResetPassword").Unprotect(Convert.FromBase64String(t));

            using var ms = new MemoryStream(bytes);
            using var br = new BinaryReader(ms);
            var dt = DateTime.FromBinary(br.ReadInt64());
            if (dt < DateTime.UtcNow)
                return Error(ExtensionsTexts.Validation.InvalidResetToken.ToString(localizer));

            userId = new TUserRow().IdField.ConvertValue(br.ReadString(), CultureInfo.InvariantCulture);
            nonce = br.ReadInt32();
        }
        catch (Exception)
        {
            return Error(ExtensionsTexts.Validation.InvalidResetToken.ToString(localizer));
        }

        using (var connection = sqlConnections.NewFor<TUserRow>())
        {
            var user = connection.TryById<TUserRow>(userId);
            if (user == null || nonce != GetNonceFor(user))
                return Error(ExtensionsTexts.Validation.InvalidResetToken.ToString(localizer));
        }

        return this.PanelPage(GetResetPasswordPageModel(t, options.Value));
    }

    protected virtual ModulePageModel GetResetPasswordPageModel(string token, MembershipSettings settings)
    {
        return new()
        {
            Module = ModulePath(nameof(ResetPassword)),
            PageTitle = ExtensionsTexts.Forms.Membership.ResetPassword.FormTitle,
            Layout = "_LayoutNoNavigation",
            Options = new
            {
                token,
                minPasswordLength = settings.MinPasswordLength
            }
        };
    }

    [HttpPost, JsonRequest]
    public virtual Result<ResetPasswordResponse> ResetPassword(ResetPasswordRequest request,
        [FromServices] ITwoLevelCache cache,
        [FromServices] ISqlConnections sqlConnections,
        [FromServices] ITextLocalizer localizer,
        [FromServices] IOptions<EnvironmentSettings> environmentOptions,
        [FromServices] IOptions<MembershipSettings> membershipOptions)
    {
        return this.InTransaction(GetConnectionKey(), uow =>
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrEmpty(request.Token))
                throw new ArgumentNullException(nameof(request.Token));

            var bytes = HttpContext.RequestServices
                .GetDataProtector("ResetPassword").Unprotect(Convert.FromBase64String(request.Token));

            object userId;
            int nonce;
            using (var ms = new MemoryStream(bytes))
            using (var br = new BinaryReader(ms))
            {
                var dt = DateTime.FromBinary(br.ReadInt64());
                if (dt < DateTime.UtcNow)
                    throw new ValidationError(ExtensionsTexts.Validation.InvalidResetToken.ToString(localizer));

                userId = new TUserRow().IdField.ConvertValue(br.ReadString(), CultureInfo.InvariantCulture);
                nonce = br.ReadInt32();
            }

            if (sqlConnections is null)
                throw new ArgumentNullException(nameof(sqlConnections));

            TUserRow user = uow.Connection.TryById<TUserRow>(userId);
            if (user == null || nonce != GetNonceFor(user))
                throw new ValidationError(ExtensionsTexts.Validation.InvalidResetToken.ToString(localizer));

            if (request.ConfirmPassword != request.NewPassword)
                throw new ValidationError("PasswordConfirmMismatch", localizer.Get("Validation.PasswordConfirm"));

            request.NewPassword = ValidateNewPassword(request.NewPassword, membershipOptions.Value, localizer);

            var salt = GenerateSalt(membershipOptions.Value);
            var hash = CalculateHash(request.NewPassword, salt);
#if (IsPublicDemo)
            if (user.IdField.AsObject(user)?.ToString() == "1")
                throw new ValidationError("Sorry, but no changes are allowed in public demo on ADMIN user!");
#endif
            var row = new TUserRow();
            row.IdField.AsObject(row, user.IdField.AsObject(user));
            if (row is IUpdateLogRow updateLogRow)
                updateLogRow.UpdateDateField[row] = DateTime.UtcNow;
            row.PasswordHashField[row] = hash;
            row.PasswordSaltField[row] = salt;
            uow.Connection.UpdateById(row);

            cache.InvalidateOnCommit(uow, row.Fields);

            return new ResetPasswordResponse
            {
                RedirectHome = User.IsLoggedIn()
            };
        });
    }
}