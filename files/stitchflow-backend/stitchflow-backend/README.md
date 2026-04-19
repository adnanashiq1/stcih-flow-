# StitchFlow — Tailor Management System
**Full-Stack: React Frontend + ASP.NET Core Backend + PostgreSQL**

---

## 🏗️ Project Structure

```
StitchFlow/
├── stitchflow.html              ← Complete Frontend (single HTML file)
└── stitchflow-backend/
    ├── Controllers/
    │   └── Controllers.cs       ← All API Controllers (Auth, Orders, Designs, etc.)
    ├── Models/
    │   └── Models.cs            ← All Entity Models
    ├── DTOs/
    │   └── DTOs.cs              ← Request/Response DTOs
    ├── Data/
    │   └── AppDbContext.cs      ← Entity Framework DbContext
    ├── Services/
    │   ├── AuthService.cs       ← JWT Auth, Register, Login
    │   ├── OrderService.cs      ← Order CRUD + Status tracking
    │   └── OtherServices.cs     ← Measurements, Designs, Notifications, Analytics
    ├── Middleware/
    │   └── ExceptionMiddleware.cs ← Global error handling
    ├── Database/
    │   └── schema.sql           ← Raw SQL schema (PostgreSQL)
    ├── Program.cs               ← App startup + DI + Middleware
    └── appsettings.json         ← Configuration
```

---

## 🛠️ Tech Stack

| Layer       | Technology                         |
|-------------|-------------------------------------|
| Frontend    | React + Tailwind CSS (or this HTML) |
| Backend     | ASP.NET Core 8 Web API (C#)         |
| Database    | PostgreSQL 15+                      |
| Auth        | JWT Bearer Tokens + Refresh Tokens  |
| ORM         | Entity Framework Core 8             |
| Image Upload| Cloudinary                          |
| Logging     | Serilog                             |
| Docs        | Swagger / OpenAPI                   |

---

## ⚡ Quick Setup

### 1. Install Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL 15+](https://www.postgresql.org/download/)

### 2. Create the Database
```bash
# In psql or pgAdmin, create the database:
CREATE DATABASE stitchflow_db;

# Then run the schema:
psql -U postgres -d stitchflow_db -f Database/schema.sql
```

### 3. Configure the Connection String
Edit `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=stitchflow_db;Username=postgres;Password=YOUR_PASSWORD"
  },
  "Jwt": {
    "Key": "YourSuperSecretKey_AtLeast32Characters_ChangeThis!",
    "Issuer": "StitchFlow"
  }
}
```

### 4. Run EF Migrations (alternative to raw SQL)
```bash
cd stitchflow-backend
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 5. Start the API
```bash
dotnet run
```
API runs at: `https://localhost:7000`
Swagger UI:  `https://localhost:7000/swagger`

---

## 📡 Complete API Reference

### Auth
| Method | Endpoint                   | Access  | Description              |
|--------|----------------------------|---------|--------------------------|
| POST   | /api/auth/register/tailor  | Public  | Register tailor account  |
| POST   | /api/auth/register/customer| Public  | Register customer account|
| POST   | /api/auth/login            | Public  | Login, get JWT tokens    |
| POST   | /api/auth/refresh          | Public  | Refresh access token     |
| POST   | /api/auth/logout           | Auth    | Revoke refresh token     |

### Orders
| Method | Endpoint                      | Access   | Description                   |
|--------|-------------------------------|----------|-------------------------------|
| POST   | /api/orders                   | Tailor   | Create new order              |
| PUT    | /api/orders/{id}              | Tailor   | Update status, note, dates    |
| GET    | /api/orders/{id}              | Auth     | Get order by ID               |
| GET    | /api/orders/track/{number}    | Public   | Track by order number (SF-1031)|
| GET    | /api/orders/tailor?status=    | Tailor   | Get tailor's orders (filtered)|
| GET    | /api/orders/customer          | Customer | Get customer's own orders     |

### Measurements
| Method | Endpoint                              | Access   | Description                 |
|--------|---------------------------------------|----------|-----------------------------|
| POST   | /api/measurements                     | Tailor   | Save customer measurements  |
| GET    | /api/measurements/customer/{id}       | Tailor   | Get measurement history     |
| GET    | /api/measurements/me?shopCode=        | Customer | View own measurements       |

### Designs
| Method | Endpoint                          | Access   | Description              |
|--------|-----------------------------------|----------|--------------------------|
| POST   | /api/designs                      | Tailor   | Create design            |
| PUT    | /api/designs/{id}                 | Tailor   | Update design            |
| DELETE | /api/designs/{id}                 | Tailor   | Remove design (soft)     |
| GET    | /api/designs/tailor               | Tailor   | My designs (with filters)|
| GET    | /api/designs?shopCode=&category=  | Public   | Browse designs           |

### Customers
| Method | Endpoint                        | Access | Description              |
|--------|---------------------------------|--------|--------------------------|
| GET    | /api/customers                  | Tailor | List linked customers    |
| POST   | /api/customers/link/{customerId}| Tailor | Link customer to shop    |

### Payments
| Method | Endpoint                    | Access   | Description                |
|--------|-----------------------------|----------|----------------------------|
| POST   | /api/payments               | Tailor   | Record payment             |
| GET    | /api/payments/order/{id}    | Auth     | Payments for an order      |
| GET    | /api/payments/tailor        | Tailor   | All tailor payments        |

### Notifications
| Method | Endpoint                        | Access | Description          |
|--------|---------------------------------|--------|----------------------|
| GET    | /api/notifications              | Auth   | Get my notifications |
| PUT    | /api/notifications/{id}/read    | Auth   | Mark one as read     |
| PUT    | /api/notifications/read-all     | Auth   | Mark all as read     |

### Analytics
| Method | Endpoint       | Access | Description            |
|--------|----------------|--------|------------------------|
| GET    | /api/analytics | Tailor | Full analytics report  |

### Reviews
| Method | Endpoint                       | Access   | Description             |
|--------|--------------------------------|----------|-------------------------|
| POST   | /api/reviews                   | Customer | Submit review (★1–5)    |
| GET    | /api/reviews/tailor/{tailorId} | Public   | Get tailor reviews      |

---

## 🗄️ Database Schema

### Tables
- **users** — All users (tailors & customers)
- **tailor_profiles** — Tailor shop details (shop name, specialization, shop code)
- **customer_tailor_links** — Many-to-many: customers linked to tailors
- **measurements** — Measurement records (full history preserved)
- **designs** — Tailor's design catalog
- **design_images** — Multiple images per design
- **orders** — Orders with status tracking
- **order_status_history** — Full audit trail of status changes
- **payments** — Payment records per order
- **notifications** — In-app notifications for both roles
- **reviews** — Customer reviews (★ 1–5)
- **refresh_tokens** — JWT refresh token management

---

## 🔐 Authentication Flow

```
1. POST /api/auth/login
   → Returns: { accessToken, refreshToken, expiresAt, user }

2. Use accessToken in headers:
   Authorization: Bearer <accessToken>

3. When token expires, POST /api/auth/refresh
   → Body: { refreshToken: "..." }
   → Returns new accessToken + refreshToken

4. POST /api/auth/logout to revoke refresh token
```

---

## 📊 Status Workflow

```
Order Created
    ↓
pending → in_progress → cutting → stitching → finishing → ready → delivered
```

Each transition:
- Is logged in `order_status_history`
- Sends a notification to the customer (if `notifyCustomer: true`)

---

## 💡 Future Enhancements
- [ ] SMS via Twilio (replace console notifications)
- [ ] Email via SendGrid
- [ ] Image uploads via Cloudinary (service stub ready)
- [ ] Admin panel for platform management
- [ ] React Native mobile app
- [ ] QR code generation for shop codes
- [ ] PDF receipt generation
- [ ] AI body measurement suggestions

---

## 🚀 Deployment

### Backend (Azure / AWS / Railway)
```bash
dotnet publish -c Release -o ./publish
# Deploy ./publish folder to your server
```

### Database
Use **Neon.tech** (free PostgreSQL) or **Supabase** for easy cloud deployment.

### Environment Variables (Production)
```
ConnectionStrings__DefaultConnection=Host=...
Jwt__Key=YourProductionSecretKey
Cloudinary__CloudName=...
```
