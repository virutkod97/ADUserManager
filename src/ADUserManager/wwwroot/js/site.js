(function () {
  'use strict';

  document.addEventListener('submit', function (e) {
    var f = e.target;
    var msg = f.getAttribute && f.getAttribute('data-confirm');
    if (msg && !window.confirm(msg)) e.preventDefault();
  }, true);

  function generatePassword(len) {
    var sets = ['ABCDEFGHJKLMNPQRSTUVWXYZ', 'abcdefghijkmnpqrstuvwxyz', '23456789', '!@#$%&*?-_+='];
    var all = sets.join('');
    var rnd = new Uint32Array(len + sets.length);
    crypto.getRandomValues(rnd);
    var chars = sets.map(function (s, i) { return s[rnd[i] % s.length]; });
    for (var i = sets.length; i < len; i++) chars.push(all[rnd[i] % all.length]);
    var sh = new Uint32Array(chars.length);
    crypto.getRandomValues(sh);
    for (var j = chars.length - 1; j > 0; j--) {
      var k = sh[j] % (j + 1);
      var t = chars[j]; chars[j] = chars[k]; chars[k] = t;
    }
    return chars.join('');
  }

  document.querySelectorAll('[data-generate]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var input = document.querySelector(btn.getAttribute('data-generate'));
      if (input) { input.value = generatePassword(12); input.dispatchEvent(new Event('input')); }
    });
  });

  document.querySelectorAll('[data-copy]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var input = document.querySelector(btn.getAttribute('data-copy'));
      if (!input || !input.value) return;
      var done = function () { var t = btn.textContent; btn.textContent = 'Đã sao chép'; setTimeout(function () { btn.textContent = t; }, 1500); };
      if (navigator.clipboard && window.isSecureContext) {
        navigator.clipboard.writeText(input.value).then(done);
      } else {
        input.removeAttribute('disabled'); input.select(); document.execCommand('copy'); done();
      }
    });
  });

  document.querySelectorAll('[data-filter-target]').forEach(function (input) {
    var list = document.querySelector(input.getAttribute('data-filter-target'));
    input.addEventListener('input', function () {
      var q = input.value.trim().toLowerCase();
      list.querySelectorAll('[data-text]').forEach(function (el) {
        el.style.display = !q || el.getAttribute('data-text').indexOf(q) >= 0 ? '' : 'none';
      });
    });
  });

  document.querySelectorAll('[data-toggle-target]').forEach(function (cb) {
    var target = document.querySelector(cb.getAttribute('data-toggle-target'));
    var sync = function () { if (target) target.hidden = !cb.checked; };
    cb.addEventListener('change', sync);
    sync();
  });

  document.querySelectorAll('[data-autosubmit]').forEach(function (el) {
    el.addEventListener('change', function () { if (el.form) el.form.submit(); });
  });

  var bulkForm = document.getElementById('bulk-form');
  if (bulkForm) {
    var selects = Array.prototype.slice.call(document.querySelectorAll('[data-bulk-rule]'));
    var countEl = bulkForm.querySelector('[data-bulk-count]');
    var changed = function () { return selects.filter(function (s) { return s.value !== s.getAttribute('data-original'); }); };
    var refresh = function () {
      var c = changed();
      selects.forEach(function (s) {
        var isChanged = c.indexOf(s) >= 0;
        s.classList.toggle('changed', isChanged);
        var tr = s.closest('tr');
        if (tr) tr.classList.toggle('row-changed', isChanged);
      });
      countEl.textContent = c.length;
      bulkForm.hidden = c.length === 0;
    };
    selects.forEach(function (s) { s.addEventListener('change', refresh); });
    bulkForm.querySelector('[data-bulk-reset]').addEventListener('click', function () {
      selects.forEach(function (s) { s.value = s.getAttribute('data-original'); });
      refresh();
    });
    bulkForm.addEventListener('submit', function (e) {
      var c = changed();
      if (c.length === 0 || !window.confirm('Đổi rule chính cho ' + c.length + ' tài khoản?')) { e.preventDefault(); return; }
      selects.forEach(function (s) { if (c.indexOf(s) < 0) s.disabled = true; });
      window.onbeforeunload = null;
    });
    window.onbeforeunload = function () { return changed().length > 0 ? '' : undefined; };
    refresh();
  }

  // Sắp xếp bảng khi bấm tiêu đề cột: lần 1 A→Z, lần 2 Z→A; ô trống luôn nằm cuối
  document.querySelectorAll('table[data-sortable]').forEach(function (table) {
    var collator = new Intl.Collator('vi', { numeric: true, sensitivity: 'base' });
    var cellValue = function (td) {
      if (!td) return '';
      if (td.hasAttribute('data-sort')) return td.getAttribute('data-sort');
      var sel = td.querySelector('select');
      if (sel) return sel.value ? sel.options[sel.selectedIndex].text : '';
      return td.textContent.trim();
    };
    var headers = table.querySelectorAll('thead th[data-sort-type]');
    headers.forEach(function (th) {
      th.classList.add('sortable');
      th.setAttribute('role', 'button');
      th.setAttribute('tabindex', '0');
      th.title = 'Bấm để sắp xếp';
      var sort = function () {
        var idx = Array.prototype.indexOf.call(th.parentNode.children, th);
        var dir = th.getAttribute('aria-sort') === 'ascending' ? -1 : 1;
        headers.forEach(function (h) { h.removeAttribute('aria-sort'); });
        th.setAttribute('aria-sort', dir === 1 ? 'ascending' : 'descending');
        var numeric = th.getAttribute('data-sort-type') === 'number';
        var tbody = table.tBodies[0];
        var rows = Array.prototype.slice.call(tbody.rows);
        rows.sort(function (a, b) {
          var x = cellValue(a.cells[idx]), y = cellValue(b.cells[idx]);
          if (x === '' && y === '') return 0;
          if (x === '') return 1;
          if (y === '') return -1;
          var r = numeric ? (parseFloat(x) - parseFloat(y)) : collator.compare(x, y);
          return r * dir;
        });
        rows.forEach(function (r) { tbody.appendChild(r); });
      };
      th.addEventListener('click', sort);
      th.addEventListener('keydown', function (e) { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); sort(); } });
    });
  });

  var primarySel = document.querySelector('[data-primary-select]');
  if (primarySel) {
    var boxes = Array.prototype.slice.call(document.querySelectorAll('#group-list input[type=checkbox]'));
    var firstTicked = function () {
      var c = boxes.filter(function (b) { return b.checked && b.getAttribute('data-can-primary') === '1'; });
      return c.length ? c[0].value : '';
    };
    boxes.forEach(function (b) {
      b.addEventListener('change', function () {
        if (b.checked && primarySel.value === '' && b.getAttribute('data-can-primary') === '1') primarySel.value = b.value;
        else if (!b.checked && primarySel.value === b.value) primarySel.value = firstTicked();
      });
    });
  }

  var kindRadios = document.querySelectorAll('[data-kind-radio]');
  if (kindRadios.length) {
    var syncKind = function () {
      var checked = document.querySelector('[data-kind-radio]:checked');
      var isPermission = checked && checked.value === 'Permission';
      document.querySelectorAll('[data-main-only]').forEach(function (el) { el.hidden = isPermission; });
    };
    kindRadios.forEach(function (r) { r.addEventListener('change', syncKind); });
    syncKind();
  }

  var form = document.getElementById('create-user-form');
  if (form) {
    var ruleData = JSON.parse(document.getElementById('rule-data').textContent || '{}');
    var ruleSelect = document.getElementById('rule-select');
    var preview = document.getElementById('rule-preview');
    var surname = document.getElementById('f-surname');
    var given = document.getElementById('f-given');
    var display = document.getElementById('f-display');
    var sam = document.getElementById('f-sam');
    var upnUser = document.getElementById('f-upn-user');
    var displayTouched = !!display.value, samTouched = !!sam.value;

    var showRule = function () {
      var r = ruleData[ruleSelect.value];
      preview.hidden = !r;
      if (!r) return;
      preview.querySelector('[data-k=ou]').textContent = r.ou;
      preview.querySelector('[data-k=groups]').textContent = r.groups.length ? r.groups.join(', ') : '(không có)';
      preview.querySelector('[data-k=probation]').textContent = r.probation
        ? '⚠ Rule thử việc: sau ' + r.days + ' ngày phần mềm sẽ nhắc chuyển rule.' : '';
    };
    ruleSelect.addEventListener('change', showRule);
    showRule();

    var strip = function (s) {
      return s.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D');
    };
    var suggestSam = function () {
      var g = strip(given.value.trim()).toLowerCase().replace(/[^a-z0-9]/g, '');
      var initials = strip(surname.value.trim()).toLowerCase().split(/\s+/)
        .filter(Boolean).map(function (w) { return w[0]; }).join('').replace(/[^a-z0-9]/g, '');
      return (g + initials).substring(0, 20);
    };
    var sync = function () {
      if (!displayTouched) display.value = [surname.value.trim(), given.value.trim()].filter(Boolean).join(' ');
      if (!samTouched) sam.value = suggestSam();
      upnUser.value = sam.value;
    };
    surname.addEventListener('input', sync);
    given.addEventListener('input', sync);
    display.addEventListener('input', function () { displayTouched = !!display.value; });
    sam.addEventListener('input', function () { samTouched = !!sam.value; upnUser.value = sam.value; });
    upnUser.value = sam.value;
  }
})();
