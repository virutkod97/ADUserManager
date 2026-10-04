# AD User Manager

Website chạy dạng **Windows Service** trên máy chủ Active Directory, hỗ trợ quản trị viên quản lý tài khoản người dùng.

## Chức năng

| Chức năng | Mô tả |
|---|---|
| **Đăng nhập bằng tài khoản AD** | Chỉ tài khoản thuộc nhóm **Administrators** (BUILTIN\Administrators, gồm cả Domain Admins / Enterprise Admins) mới đăng nhập được. Kiểm tra thành viên đệ quy. Sai mật khẩu 5 lần trong 10 phút → chặn 5 phút. |
| **Quản lý rule tài khoản** | Mỗi rule map tới **1 OU** và **nhiều group**. Tạo user theo rule → user được tạo trong OU đó và thêm vào các group đó. |
| **Rule thử việc** | Tick "Rule thử việc" + số ngày (mặc định **7**). Hết hạn → cảnh báo ở menu (badge số), banner trên mọi trang, danh sách ở Tổng quan, đánh dấu ở danh sách tài khoản và trang chi tiết. |
| **Chuyển rule** | Chuyển user sang rule khác: di chuyển OU, thêm group rule mới, gỡ group rule cũ (tuỳ chọn từng bước). Cũng dùng để gán rule cho tài khoản có sẵn. |
| **Thêm / sửa / xoá tài khoản** | Tự sinh tên hiển thị và tên đăng nhập từ họ tên tiếng Việt (VD: *Nguyễn Đức Anh* → `anhnd`), chọn UPN suffix, thông tin phòng ban, chức danh, mã NV... Vô hiệu hoá/kích hoạt, mở khoá. |
| **Reset mật khẩu** | Sinh mật khẩu ngẫu nhiên đủ độ phức tạp, tuỳ chọn bắt đổi mật khẩu lần đăng nhập tới, mở khoá tài khoản. |
| **Nhật ký thao tác** | Ghi lại ai làm gì, lúc nào, từ IP nào, thành công hay lỗi. |

## Kiến trúc

- ASP.NET Core 8 (Razor Pages), chạy dưới dạng Windows Service (Kestrel, HTTPS).
- Thao tác AD qua `System.DirectoryServices` / `System.DirectoryServices.AccountManagement` (LDAP có ký + mã hoá Kerberos).
- Dữ liệu rule, gán rule, nhật ký lưu trong SQLite: `<thư mục cài đặt>\data\adusermanager.db`.
- Mọi thao tác AD chạy bằng danh tính của service (mặc định **LocalSystem** — trên Domain Controller tương đương quyền quản trị domain). Người đăng nhập web được ghi vào nhật ký thao tác.

```
src/ADUserManager/
├── Program.cs                 # cấu hình host, Windows Service, auth cookie, DI
├── Services/
│   ├── AdService.cs           # thao tác AD thật (Windows)
│   ├── MockAdService.cs       # AD giả lập để chạy thử khi phát triển
│   ├── RuleService.cs         # nghiệp vụ rule, chuyển rule, theo dõi thử việc
│   └── AuditService.cs        # nhật ký thao tác
├── Data/                      # EF Core + SQLite (AccountRule, UserRuleAssignment, AuditLog)
├── Pages/                     # giao diện (Tổng quan, Tài khoản, Rule, Nhật ký, Đăng nhập)
└── wwwroot/                   # CSS/JS (không phụ thuộc CDN, chạy được trong mạng nội bộ)
scripts/
├── publish.ps1                # build bản self-contained win-x64
├── install-service.ps1        # cài/nâng cấp service trên máy chủ AD
└── uninstall-service.ps1
```

## Cài đặt

### 1. Lấy bản build

- Tải artifact **ADUserManager-win-x64** ở tab *Actions* của repo (tự build mỗi lần push), hoặc
- Tự build trên máy có .NET 8 SDK: `.\scripts\publish.ps1` → thư mục `publish\`.

Bản build là 1 file `ADUserManager.exe` **self-contained** — máy chủ AD **không cần cài .NET**.

### 2. Cài lên máy chủ AD

Copy thư mục lên máy chủ AD (Domain Controller hoặc máy đã join domain) và **double-click `install.bat`** (tự xin quyền Administrator).

Tuỳ chọn (chạy trong cmd Administrator): `install.bat -Port 8443 -InstallDir "D:\Apps\ADUserManager"`

Script sẽ:
1. Copy vào `C:\Program Files\ADUserManager` (khi nâng cấp **giữ nguyên** `appsettings*.json` và thư mục `data`).
2. Tạo chứng chỉ HTTPS tự ký `CN=ADUserManager` trong `LocalMachine\My` (nếu chưa có).
3. Phân quyền thư mục `data` và file cấu hình chỉ cho SYSTEM + Administrators.
4. Tạo service `ADUserManager` (Automatic, tự khởi động lại khi lỗi), mở firewall (Domain profile).

Truy cập: `https://<tên-máy-chủ>:5443`

> **Chứng chỉ:** chứng chỉ tự ký sẽ bị trình duyệt cảnh báo. Nên dùng chứng chỉ do CA nội bộ (AD CS) cấp: import vào `LocalMachine\My` rồi sửa `Certificate.Subject` trong `appsettings.Production.json` cho khớp, restart service.

### 3. Gỡ cài đặt

```bat
uninstall.bat                 :: giữ lại dữ liệu
uninstall.bat -RemoveFiles    :: xoá toàn bộ (chạy trong cmd Administrator)
```

## Cấu hình (`appsettings.json`)

```jsonc
"ActiveDirectory": {
  "Domain": "",                      // để trống = domain của máy chủ; hoặc "corp.local" / "dc01.corp.local"
  "Username": "",                    // để trống = dùng danh tính service (khuyến nghị)
  "Password": "",
  "AdminGroups": [ "S-1-5-32-544" ], // group được phép đăng nhập (SID, tên hoặc DN)
  "MaxSearchResults": 1000
},
"App": { "SessionTimeoutMinutes": 30 }
```

- **Chạy service trên máy không phải DC:** đổi "Log On" của service sang một tài khoản/gMSA đã được **uỷ quyền** (Delegate Control) trên các OU cần quản lý (tạo/xoá/sửa user, reset password, sửa thành viên group), hoặc điền `Username`/`Password` (file cấu hình đã được phân quyền chỉ Administrators đọc được).
- **Giới hạn người đăng nhập:** đổi `AdminGroups`, VD: `[ "Domain Admins", "IT-Helpdesk" ]`.
- **Cổng/HTTPS:** `appsettings.Production.json` → `Kestrel:Endpoints`.
- Log lỗi: **Event Viewer → Windows Logs → Application**, nguồn `ADUserManager`.

## Cách hoạt động của cảnh báo thử việc

- Khi tạo user theo rule hoặc chuyển rule, phần mềm lưu *thời điểm user vào rule* (bảng `Assignments`).
- Nếu rule có tick **thử việc**, hạn = thời điểm vào rule + số ngày thử việc của rule.
- Đến hạn: badge đỏ "Quá hạn thử việc" + banner nhắc chuyển rule. Bấm **Chuyển rule** → chọn rule chính thức → user được chuyển OU/group, cảnh báo biến mất.
- Tài khoản tạo ngoài phần mềm có thể **gán rule** ở trang chi tiết tài khoản để bắt đầu theo dõi.

## Phát triển / chạy thử không cần AD

```bash
cd src/ADUserManager
ASPNETCORE_ENVIRONMENT=Development dotnet run    # http://localhost:5080
```

Ở môi trường Development dùng AD giả lập (`ActiveDirectory:UseMock=true` trong `appsettings.Development.json`):
`admin` / `Admin@123` (có quyền), `user01` / `User@123` (không có quyền). AD giả lập không bao giờ được bật ở Production.
