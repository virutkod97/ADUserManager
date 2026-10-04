## Cài mới
1. Tải `ADUserManager-x.y.z-win-x64.zip`, giải nén lên máy chủ AD.
2. Double-click `install.bat`.
3. Truy cập `https://<tên-máy-chủ>:5443`.

## Nâng cấp
- Trong phần mềm: menu **Cập nhật** → **Cập nhật ngay**, hoặc
- Giải nén bản mới và chạy lại `install.bat`. Cấu hình và dữ liệu được giữ nguyên.

## Thay đổi trong bản này
- Sửa lỗi đổi rule chính không gỡ được group cũ trên AD mà không báo lỗi. Thêm/gỡ group giờ ghi trực tiếp lên group
  (IADsGroup Add/Remove, không phụ thuộc số thành viên) và **đọc lại để xác nhận**; nếu AD không gỡ/thêm được sẽ báo cảnh báo rõ ràng.
- Thông báo sau khi đổi rule ghi rõ từng tài khoản đã gỡ những group nào.
