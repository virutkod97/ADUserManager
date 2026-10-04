## Cài mới
1. Tải `ADUserManager-x.y.z-win-x64.zip`, giải nén lên máy chủ AD.
2. Double-click `install.bat`.
3. Truy cập `https://<tên-máy-chủ>:5443`.

## Nâng cấp
- Trong phần mềm: menu **Cập nhật** → **Cập nhật ngay**, hoặc
- Giải nén bản mới và chạy lại `install.bat`. Cấu hình và dữ liệu được giữ nguyên.

## Thay đổi trong bản này
- **Rule phân quyền**: loại rule mới chỉ thêm group, không đổi OU; một tài khoản gán được nhiều rule phân quyền
  (VD: rule chính *Quản trị* → OU Quản trị + group IT; thêm rule phân quyền *Trưởng bộ phận* (TBP) và *Pháp chế*).
- Gán rule phân quyền khi tạo tài khoản hoặc ở trang chi tiết tài khoản; gỡ rule chỉ gỡ những group không còn rule nào khác cấp.
- Chuyển rule chính không gỡ các group mà rule phân quyền đang cấp.
- Danh sách tài khoản hiển thị và lọc theo rule phân quyền.
- Tự nâng cấp cơ sở dữ liệu của bản cũ, giữ nguyên rule và dữ liệu đã có.
