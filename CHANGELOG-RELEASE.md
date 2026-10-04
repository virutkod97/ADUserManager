## Cài mới
1. Tải `ADUserManager-x.y.z-win-x64.zip`, giải nén lên máy chủ AD.
2. Double-click `install.bat`.
3. Truy cập `https://<tên-máy-chủ>:5443`.

## Nâng cấp
- Trong phần mềm: menu **Cập nhật** → **Cập nhật ngay**, hoặc
- Giải nén bản mới và chạy lại `install.bat`. Cấu hình và dữ liệu được giữ nguyên.

## Thay đổi trong bản này
- **Bảo vệ tài khoản administrator**: tài khoản `administrator` và tài khoản quản trị gốc của domain (RID 500, kể cả khi đã đổi tên)
  bị ẩn khỏi trang Tài khoản và bị chặn mọi thao tác qua phần mềm (sửa, reset mật khẩu, xoá, vô hiệu hoá, đổi rule/group).
  Vẫn đăng nhập phần mềm bằng tài khoản này được.
- **Đếm lại thử việc**: nút "Đếm lại thử việc" ở trang Tổng quan và trang Sửa tài khoản, đặt ngày bắt đầu thử việc về hôm nay
  (0/7 ngày) mà không đổi OU/group. Có ghi nhật ký.
