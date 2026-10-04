(function () {
  'use strict';

  // Xác nhận trước khi submit form có data-confirm
  document.addEventListener('submit', function (e) {
    var f = e.target;
    var msg = f.getAttribute && f.getAttribute('data-confirm');
    if (msg && !window.confirm(msg)) e.preventDefault();
  }, true);

  // Sinh mật khẩu ngẫu nhiên đáp ứng độ phức tạp của AD
  function generatePassword(len) {
    var sets = ['ABCDEFGHJKLMNPQRSTUVWXYZ', 'abcdefghijkmnpqrstuvwxyz', '23456789', '!@#$%&*?-_+='];
    var all = sets.join('');
    var rnd = new Uint32Array(len + sets.length);
    crypto.getRandomValues(rnd);
    var chars = sets.map(function (s, i) { return s[rnd[i] % s.length]; });
    for (var i = sets.length; i < len; i++) chars.push(all[rnd[i] % all.length]);
    // trộn Fisher–Yates
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

  // Lọc danh sách checkbox
  document.querySelectorAll('[data-filter-target]').forEach(function (input) {
    var list = document.querySelector(input.getAttribute('data-filter-target'));
    input.addEventListener('input', function () {
      var q = input.value.trim().toLowerCase();
      list.querySelectorAll('[data-text]').forEach(function (el) {
        el.style.display = !q || el.getAttribute('data-text').indexOf(q) >= 0 ? '' : 'none';
      });
    });
  });

  // Ẩn/hiện phần tử theo checkbox
  document.querySelectorAll('[data-toggle-target]').forEach(function (cb) {
    var target = document.querySelector(cb.getAttribute('data-toggle-target'));
    var sync = function () { if (target) target.hidden = !cb.checked; };
    cb.addEventListener('change', sync);
    sync();
  });

  // ---------------- Trang tạo tài khoản
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

    // Bỏ dấu tiếng Việt
    var strip = function (s) {
      return s.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D');
    };
    // Nguyễn Đức + Anh => anhnd
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
