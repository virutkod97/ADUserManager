## Cài mới
1. Tải `ADUserManager-x.y.z-win-x64.zip`, giải nén lên máy chủ AD.
2. Double-click `install.bat`.
3. Truy cập `https://<tên-máy-chủ>:5443`.

## Nâng cấp
- Trong phần mềm: menu **Cập nhật** → **Cập nhật ngay**, hoặc
- Giải nén bản mới và chạy lại `install.bat`. Cấu hình và dữ liệu được giữ nguyên.

## Thay đổi trong bản này
- Đơn giản hoá đổi rule chính hàng loạt: bỏ các ô tuỳ chọn. Chọn rule chính B cho tài khoản đang ở A rồi bấm **Lưu**
  → tài khoản chuyển sang OU của B, bỏ group của A, thêm group của B. Group do rule phân quyền cấp được giữ nguyên.
  Cần tuỳ chỉnh chi tiết thì vào **Sửa** từng tài khoản.
