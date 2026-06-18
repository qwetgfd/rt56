(function () {
  function showModal(modalEl) {
    if (!modalEl || modalEl.classList.contains('show')) {
      return;
    }

    modalEl.classList.add('show');
    modalEl.style.display = 'block';
    modalEl.removeAttribute('aria-hidden');
    modalEl.setAttribute('aria-modal', 'true');
    document.body.classList.add('modal-open');
    document.body.style.overflow = 'hidden';

    if (!document.querySelector('.modal-backdrop')) {
      var backdrop = document.createElement('div');
      backdrop.className = 'modal-backdrop fade show';
      document.body.appendChild(backdrop);
    }
  }

  function hideModal(modalEl) {
    if (!modalEl) {
      return;
    }

    modalEl.classList.remove('show');
    modalEl.style.display = 'none';
    modalEl.setAttribute('aria-hidden', 'true');
    modalEl.removeAttribute('aria-modal');
    document.body.classList.remove('modal-open');
    document.body.style.removeProperty('overflow');

    document.querySelectorAll('.modal-backdrop').forEach(function (node) {
      node.remove();
    });
  }

  function toggleDropdown(trigger) {
    if (!trigger) {
      return;
    }

    var menu = trigger.parentElement
      ? trigger.parentElement.querySelector('.dropdown-menu')
      : null;

    if (!menu) {
      return;
    }

    var isOpen = menu.classList.contains('show');
    document.querySelectorAll('.dropdown-menu.show').forEach(function (node) {
      node.classList.remove('show');
    });
    document.querySelectorAll('[data-bs-toggle="dropdown"][aria-expanded="true"]').forEach(function (node) {
      node.setAttribute('aria-expanded', 'false');
    });

    if (!isOpen) {
      menu.classList.add('show');
      trigger.setAttribute('aria-expanded', 'true');
    }
  }

  window.tpdiShowModal = function (selector) {
    window.setTimeout(function () {
      showModal(document.querySelector(selector));
    }, 0);
  };

  document.addEventListener('click', function (event) {
    var modalTrigger = event.target.closest('[data-bs-toggle="modal"]');
    if (modalTrigger) {
      var targetSelector = modalTrigger.getAttribute('data-bs-target');
      if (targetSelector) {
        event.preventDefault();
        // Let Angular (click) handlers update modal content first.
        window.setTimeout(function () {
          showModal(document.querySelector(targetSelector));
        }, 0);
      }
      return;
    }

    var dismissTrigger = event.target.closest('[data-bs-dismiss="modal"]');
    if (dismissTrigger) {
      event.preventDefault();
      var modal = dismissTrigger.closest('.modal') || document.querySelector('.modal.show');
      hideModal(modal);
      return;
    }

    var dropdownTrigger = event.target.closest('[data-bs-toggle="dropdown"]');
    if (dropdownTrigger) {
      event.preventDefault();
      toggleDropdown(dropdownTrigger);
      return;
    }

    if (!event.target.closest('.dropdown')) {
      document.querySelectorAll('.dropdown-menu.show').forEach(function (node) {
        node.classList.remove('show');
      });
      document.querySelectorAll('[data-bs-toggle="dropdown"][aria-expanded="true"]').forEach(function (node) {
        node.setAttribute('aria-expanded', 'false');
      });
    }

    if (event.target.classList.contains('modal') && event.target.classList.contains('show')) {
      hideModal(event.target);
    }
  });
})();
