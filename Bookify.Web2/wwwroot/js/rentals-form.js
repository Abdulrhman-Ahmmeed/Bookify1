// Initialize an array to store selected copies
var selectedCopies = [];
console.log(maxAllowedCopies);

// Initialize an array to store selected copy serial numbers
var selectedCopySerials = [];

$(document).ready(function () {
    // Event handler for the search button
    $('.js-search').on('click', function (e) {
        e.preventDefault();
        var serial = $('#Value').val();
        if (selectedCopies.find(c => c.serial === serial)) {
            showAlertMessage("There is another Copy with the same serial");
            return;
        }
        if (selectedCopies.length >= maxAllowedCopies) {
            showAlertMessage(`You can't add more than ${maxAllowedCopies} books`);
            return;
        }
        // If not a duplicate and not exceeding the limit, submit the form
        $('#SearchForm').submit();
    });

    $('body').on('click', '.js-remove', function () {
        $(this).parents('.js-remove-container').remove();
        copyPreper();
        if (selectedCopies.length === 0) {
            $('#CopiesForm').find(':submit').addClass('d-none');
        }
    });

    // Event handler for when a copy is selected
    $('body').on('change', '.js-copy', function () {
        var serialNumber = $(this).val();
        console.log('Copy selected:', serialNumber);

        // Check if the serial number is already in the selected list
        if (selectedCopySerials.includes(serialNumber)) {
            // If already selected, remove it
            selectedCopySerials = selectedCopySerials.filter(serial => serial !== serialNumber);
        } else {
            // If not selected, add it
            selectedCopySerials.push(serialNumber);
        }

        // Update the hidden input field with the selected serial numbers
        $('#SelectedCopies').val(selectedCopySerials.join(',')); // Assuming SelectedCopies is the name of the input field
    });
});

function onAddCopySuccess(copy) {
    // Clear the input field after a successful copy addition
    $('#Value').val('');
    var bookId = $(copy).find('.js-copy').data('book-id');
    if (selectedCopies.find(c => c.bookId === bookId)) {
        showAlertMessage('You cannot add two copies with the same ID');
        return;
    }
    // Prepend the new copy to the CopiesForm
    $('#CopiesForm').prepend(copy);
    $('#CopiesForm').find(':submit').removeClass('d-none');
    copyPreper();
}

function copyPreper() {
    var copies = $('.js-copy');
    // Reset the selectedCopies array
    selectedCopies = [];
    // Loop through all the copies and update the selectedCopies array
    copies.each(function (i, input) {
        var $input = $(input);
        var serial = $input.val();
        var id = $input.data('book-id'); // Retrieve the book ID from the data attribute
        selectedCopies.push({ serial: serial, bookId: id });
        $input.attr('name', `SelectedCopies[${i}].serial`);
    });
}
