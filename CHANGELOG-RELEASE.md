## Cài mới
1. Tải `ADUserManager-x.y.z-win-x64.zip`, giải nén lên máy chủ AD.
2. Double-click `install.bat`.
3. Truy cập `https://<tên-máy-chủ>:5443`.

## Nâng cấp
- Trong phần mềm: menu **Cập nhật** → **Cập nhật ngay**, hoặc
- Giải nén bản mới và chạy lại `install.bat`. Cấu hình và dữ liệu được giữ nguyên.

## Thay đổi trong bản này
- Sửa lỗi đổi rule chính báo "không gỡ group nào" dù tài khoản đang thuộc group: danh sách group của tài khoản giờ được
  đọc gộp từ 3 nguồn (memberOf trong kết quả tìm kiếm, memberOf đọc trực tiếp trên tài khoản, và tìm ngược các group có
  thành viên là tài khoản), dùng cho cả thẻ Group ở trang Sửa.
- Thông báo sau khi đổi rule hiển thị: các group đọc được, group đã gỡ, group giữ lại kèm lý do (rule mới / rule phân quyền).
