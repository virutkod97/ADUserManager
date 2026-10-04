## Cài mới
1. Tải `ADUserManager-x.y.z-win-x64.zip`, giải nén lên máy chủ AD.
2. Double-click `install.bat`.
3. Truy cập `https://<tên-máy-chủ>:5443`.

## Nâng cấp
- Trong phần mềm: menu **Cập nhật** → **Cập nhật ngay**, hoặc
- Giải nén bản mới và chạy lại `install.bat`. Cấu hình và dữ liệu được giữ nguyên.

## Thay đổi trong bản này
- Nút chuyển giao diện sáng/tối (nhớ lựa chọn theo trình duyệt).
- Tự động kiểm tra phiên bản mới trên GitHub Releases (6 giờ/lần), cảnh báo trên giao diện và cập nhật bằng 1 nút (có kiểm tra SHA-256).
- Chạy lại `install.bat` không còn ghi đè cổng HTTPS đã cấu hình.
- Dọn mã nguồn.
