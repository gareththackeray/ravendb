﻿import dialogViewModelBase = require("viewmodels/dialogViewModelBase");
import dialog = require("plugins/dialog");

class searchFileSizeRangeClause extends dialogViewModelBase {

    public applyFilterTask = $.Deferred();
    minSizeText = ko.observable("");
    maxSizeText = ko.observable("");

    cancel() {
        dialog.close(this);
    }

    applyFilter() {
        var filter = "__size_numeric:[" + this.convertInputStringToRangeValue(this.minSizeText()) +
            " TO " + this.convertInputStringToRangeValue(this.maxSizeText()) + "]";
        this.applyFilterTask.resolve(filter);
        dialog.close(this);
    }

    private convertInputStringToRangeValue(input: string) : string {

        if (!input)
            return "*";

        var regex = /^(\d+)\s*(\w*)$/;
        if (!regex.test(input))
            return "*";

        var match = regex.exec(input);
        var value = parseInt(match[1]);
        var multiplier = this.getMultiplier(match[2]);

        value *= multiplier;

        return value.toString();
    }

    private getMultiplier(value: string) {

        if (!value)
            return 1;

        if (value.indexOf("k") > -1)
            return 1024;

        if (value.indexOf("m") > -1)
            return 1024 * 1024;

        if (value.indexOf("g") > -1)
            return 1024 * 1024 * 1024;

        return 1;
    }
}

export = searchFileSizeRangeClause;