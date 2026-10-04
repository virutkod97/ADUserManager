## Cài mới
1. Tải `ADUserManager-x.y.z-win-x64.zip`, giải nén lên máy chủ AD.
2. Double-click `install.bat`.
3. Truy cập `https://<tên-máy-chủ>:5443`.

## Nâng cấp
- Trong phần mềm: menu **Cập nhật** → **Cập nhật ngay**, hoặc
- Giải nén bản mới và chạy lại `install.bat`. Cấu hình và dữ liệu được giữ nguyên.

## Thay đổi trong bản này
- **Đổi rule chính giờ gỡ toàn bộ group hiện có** của tài khoản rồi mới thêm group của rule mới (áp dụng cả đổi hàng loạt
  lẫn trang Sửa). Chỉ giữ lại group do rule phân quyền cấp; Domain Users (primary group) không bị ảnh hưởng.
  Áp dụng cả với tài khoản trước đó chưa gán rule hoặc có group thêm tay ngoài phần mềm.
  Nhật ký ghi rõ danh sách group đã gỡ.
- Trang Tài khoản mặc định **chỉ hiện tài khoản đang hoạt động**; tick **Hiện cả tài khoản bị vô hiệu hoá** để xem toàn bộ.
