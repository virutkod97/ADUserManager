## Cài mới
1. Tải `ADUserManager-x.y.z-win-x64.zip`, giải nén lên máy chủ AD.
2. Double-click `install.bat`.
3. Truy cập `https://<tên-máy-chủ>:5443`.

## Nâng cấp
- Trong phần mềm: menu **Cập nhật** → **Cập nhật ngay**, hoặc
- Giải nén bản mới và chạy lại `install.bat`. Cấu hình và dữ liệu được giữ nguyên.

## Thay đổi trong bản này
- **Xử lý primary group khi đổi rule chính.** Nguyên nhân group cũ không bị gỡ: group đó đang là *primary group* của tài khoản
  (không nằm trong memberOf và AD không cho gỡ trực tiếp). Đổi rule chính giờ chạy theo thứ tự:
  1. Lưu danh sách group cần gỡ (gồm cả primary group hiện tại).
  2. Thêm các group của rule mới.
  3. Đặt primary group theo rule mới (luôn chuyển).
  4. Gỡ các group đã lưu.
- Rule chính có thêm ô **Primary group** (chỉ group bảo mật Global/Universal; để trống = Domain Users).
  Group chọn làm primary tự được thêm vào danh sách group của rule. Tài khoản tạo mới theo rule cũng được đặt primary group này.
- Thẻ Group ở trang Sửa tài khoản hiển thị cả primary group (đánh dấu "· primary").
- Thông báo/nhật ký đổi rule ghi thêm thay đổi primary group.
