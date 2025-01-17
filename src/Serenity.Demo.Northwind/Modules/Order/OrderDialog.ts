import { Decorators, EntityDialog } from "@serenity-is/corelib";
import { ReportHelper } from "@serenity-is/extensions";
import { OrderForm, OrderRow, OrderService } from "@/ServerTypes/Demo";

@Decorators.registerClass('Serenity.Demo.Northwind.OrderDialog')
@Decorators.panel()
export class OrderDialog extends EntityDialog<OrderRow, any> {
    protected getFormKey() { return OrderForm.formKey; }
    protected getRowDefinition() { return OrderRow; }
    protected getService() { return OrderService.baseUrl; }

    protected form = new OrderForm(this.idPrefix);

    constructor() {
        super();
    }

    getToolbarButtons() {
        var buttons = super.getToolbarButtons();

        buttons.push(ReportHelper.createToolButton({
            title: 'Invoice',
            cssClass: 'export-pdf-button',
            reportKey: 'Northwind.OrderDetail',
            getParams: () => ({
                OrderID: this.get_entityId()
            })
        }));

        return buttons;
    }

    protected updateInterface() {
        super.updateInterface();

        this.toolbar.findButton('export-pdf-button').toggle(this.isEditMode());
    }
}