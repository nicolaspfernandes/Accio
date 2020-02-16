﻿$(document).ready(function () {
    SetCardSets();
    InitializeElements();
});

function InitializeElements() {
    
}
function SetCardSets() {
    $.ajax({
        type: "POST",
        url: "CardBrowser?handler=GetSets",
        beforeSend: function (xhr) {
            xhr.setRequestHeader("XSRF-TOKEN",
                $('input:hidden[name="__RequestVerificationToken"]').val());
        },
        contentType: false,
        processData: false,
        success: function (response) {
            //ClearErrors(); //hide any errors from previous requests

            if (response.success) {
                var sets = response.json;
                AddSetsToDropDown(sets);
            }
            else {
                //ShowErrors(response.message, response.info);
            }

            //SetDropDownMenuButtonLoadingState('unloading');
        },
        failure: function (response) {
            alert('Catastropic error');
        }
    });
}
function AddSetsToDropDown(sets) {
    var dropDownName = '#setSelect';

    //Add empty item so the user can choose to not search by set if they want
    var emptyOption = '<option value="00000000-0000-0000-0000-000000000000">Select Set</option>';
    $(dropDownName).append(emptyOption);

    for (var i = 0; i < sets.length; i++) {
        var set = sets[i];
        var img = '/images/seticons/' + set.iconFileName;

        var option = '<option value="' + set.setId + '">' + set.name + '</option>';
        $(dropDownName).append(option);
    }
}