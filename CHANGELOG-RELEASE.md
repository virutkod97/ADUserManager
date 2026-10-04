## Cài mới
1. Tải `ADUserManager-x.y.z-win-x64.zip`, giải nén lên máy chủ AD.
2. Double-click `install.bat`.
3. Truy cập `https://<tên-máy-chủ>:5443`.

## Nâng cấp
- Trong phần mềm: menu **Cập nhật** → **Cập nhật ngay**, hoặc
- Giải nén bản mới và chạy lại `install.bat`. Cấu hình và dữ liệu được giữ nguyên.

## Thay đổi trong bản này
- Trang Tài khoản thêm cột **Đăng nhập cuối**. Thời gian lấy giá trị mới nhất giữa `lastLogon` (chính xác, của DC đang truy vấn)
  và `lastLogonTimestamp` (nhân bản giữa các DC nhưng có thể trễ 9–14 ngày). Tài khoản chưa đăng nhập hiện "Chưa đăng nhập".
