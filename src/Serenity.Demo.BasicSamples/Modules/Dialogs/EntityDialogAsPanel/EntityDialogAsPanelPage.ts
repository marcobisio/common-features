﻿import { Decorators } from "@serenity-is/corelib";
import { notifySuccess, SaveResponse } from "@serenity-is/corelib/q";
import { OrderDialog } from "@serenity-is/demo.northwind";

export default function pageInit(model: any) {

    // first create a new dialog, store it in globalThis, e.g. window so that sample can access it from outsite the module
    var myDialogAsPanel = new EntityDialogAsPanel();

    $('#SwitchToNewRecordMode').click(() => {
        myDialogAsPanel.load({}, function() { notifySuccess('Switched to new record mode'); });
    });

    $('#LoadEntityWithId').click(() => {
        myDialogAsPanel.load(11048, function() { notifySuccess('Loaded entity with ID 11048'); })
    });

    // load a new entity if url doesn't contain an ID, or load order with ID specified in page URL
    // here we use done event in second parameter, to be sure operation succeeded before showing the panel
    myDialogAsPanel.load(model || {}, function () {
        // if we didn't reach here, probably there is no order with specified ID in url
        myDialogAsPanel.element.removeClass('hidden').appendTo('#DialogDiv');
        myDialogAsPanel["arrange"]?.();
    });
}


/**
 * A version of order dialog converted to a panel by adding Serenity.@Decorators.panel decorator.
 */
@Decorators.panel()
export class EntityDialogAsPanel extends OrderDialog {

    constructor() {
        super();
    }

    protected updateInterface() {
        super.updateInterface();

        this.deleteButton.hide();
        this.applyChangesButton.hide();
    }

    protected onSaveSuccess(response: SaveResponse) {
        this.showSaveSuccessMessage(response);
    }
}