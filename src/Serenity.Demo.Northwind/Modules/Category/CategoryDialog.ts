import { Decorators, EntityDialog } from "@serenity-is/corelib";
import { CategoryForm, CategoryRow, CategoryService } from "@/ServerTypes/Demo";

@Decorators.registerClass('Serenity.Demo.Northwind.CategoryDialog')
export class CategoryDialog extends EntityDialog<CategoryRow, any> {
    protected getFormKey() { return CategoryForm.formKey; }
    protected getRowDefinition() { return CategoryRow; }
    protected getService() { return CategoryService.baseUrl; }

    protected form = new CategoryForm(this.idPrefix);
}