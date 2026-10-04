## Cài mới
1. Tải `ADUserManager-x.y.z-win-x64.zip`, giải nén lên máy chủ AD.
2. Double-click `install.bat`.
3. Truy cập `https://<tên-máy-chủ>:5443`.

## Nâng cấp
- Trong phần mềm: menu **Cập nhật** → **Cập nhật ngay**, hoặc
- Giải nén bản mới và chạy lại `install.bat`. Cấu hình và dữ liệu được giữ nguyên.

## Thay đổi trong bản này
- Primary group của rule chính mặc định lấy **group đầu tiên được tick** (Global/Universal) thay vì Domain Users;
  bỏ tick group đó thì tự chuyển sang group được tick tiếp theo; vẫn chọn tay được (kể cả Domain Users).
  Rule cũ chưa lưu primary group cũng dùng group hợp lệ đầu tiên của rule khi đổi rule / tạo tài khoản.
- Trang sửa tài khoản: nút **Chuyển rule** và **Bỏ gán rule** cùng kích thước, nằm trên một hàng.
