## Cài mới
1. Tải `ADUserManager-x.y.z-win-x64.zip`, giải nén lên máy chủ AD.
2. Double-click `install.bat`.
3. Truy cập `https://<tên-máy-chủ>:5443`.

## Nâng cấp
- Trong phần mềm: menu **Cập nhật** → **Cập nhật ngay**, hoặc
- Giải nén bản mới và chạy lại `install.bat`. Cấu hình và dữ liệu được giữ nguyên.

## Thay đổi trong bản này
- Sửa lỗi nâng cấp đè lên bản cũ không cập nhật thư mục `wwwroot` (bị chép lồng thành `wwwroot\wwwroot`), khiến giao diện vẫn dùng CSS/JS cũ và nút sáng/tối không hiển thị.
- Nút sáng/tối luôn có biểu tượng kể cả khi JavaScript chưa tải.
