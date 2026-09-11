document.addEventListener("DOMContentLoaded", function () {
  var modalElement = document.getElementById("confirmActionModal");
  var messageElement = document.getElementById("confirmActionModalMessage");
  var submitButton = document.getElementById("confirmActionModalSubmit");
  var pendingForm = null;
  var allowSubmit = false;

  if (
    !modalElement ||
    !messageElement ||
    !submitButton ||
    typeof bootstrap === "undefined"
  ) {
    return;
  }

  var confirmModal = new bootstrap.Modal(modalElement);

  document.addEventListener("submit", function (event) {
    var form = event.target;
    if (!(form instanceof HTMLFormElement)) {
      return;
    }

    var message = form.getAttribute("data-confirm-message");
    if (!message) {
      return;
    }

    if (allowSubmit) {
      allowSubmit = false;
      return;
    }

    event.preventDefault();
    pendingForm = form;
    messageElement.textContent = message;
    confirmModal.show();
  });

  submitButton.addEventListener("click", function () {
    if (!pendingForm) {
      confirmModal.hide();
      return;
    }

    allowSubmit = true;
    confirmModal.hide();
    pendingForm.requestSubmit();
    pendingForm = null;
  });

  modalElement.addEventListener("hidden.bs.modal", function () {
    pendingForm = null;
  });
});
