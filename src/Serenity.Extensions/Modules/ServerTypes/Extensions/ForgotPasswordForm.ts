﻿import { EmailAddressEditor, PrefixedContext } from "@serenity-is/corelib";
import { initFormType } from "@serenity-is/corelib/q";

export interface ForgotPasswordForm {
    Email: EmailAddressEditor;
}

export class ForgotPasswordForm extends PrefixedContext {
    static formKey = 'Serenity.Extensions.ForgotPasswordRequest';
    private static init: boolean;

    constructor(prefix: string) {
        super(prefix);

        if (!ForgotPasswordForm.init)  {
            ForgotPasswordForm.init = true;

            var w0 = EmailAddressEditor;

            initFormType(ForgotPasswordForm, [
                'Email', w0
            ]);
        }
    }
}