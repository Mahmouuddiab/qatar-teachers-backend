# Qatar Teachers Backend

REST API for a private-tutoring platform in Qatar. Students browse courses and book sessions, and teachers publish courses, manage their availability and accept or reject bookings. It is the backend for the **Darsak (درسك)** Flutter apps (student and teacher).

## Tech stack

- ASP.NET Core Web API (C#)
- Entity Framework Core with MySQL
- JWT bearer authentication, with role-based authorization (`Student`, `Teacher`)
- BCrypt for password hashing
- Swagger UI for exploring the API

## Project structure

```
QatarLearningBackend/
├── Controllers/
│   ├── AuthController.cs      # register, login, profile, password
│   ├── StudentController.cs   # browse courses, book and cancel sessions
│   └── TeacherController.cs   # dashboard, courses, availability, bookings
├── Data/                      # AppDbContext
├── DTOs/                      # request and response models
├── Models/                    # User, TeacherProfile, Course, Booking, TeacherAvailability
└── Program.cs
```

## Getting started

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (the version targeted by the `.csproj`)
- A MySQL database

### Configuration

Secrets are **not** stored in this repository. `appsettings.json` contains placeholders only. Put your real values in `appsettings.Development.json` (git-ignored) or in user-secrets:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_HOST;Database=YOUR_DB;Uid=YOUR_USER;Pwd=YOUR_PASSWORD;SslMode=Preferred;"
  },
  "Jwt": {
    "Key": "A_LONG_RANDOM_KEY_AT_LEAST_32_BYTES",
    "Issuer": "QatarLearningPlatform",
    "Audience": "QatarLearningUsers"
  }
}
```

In production, use environment variables instead (double underscore for nesting):

```
ConnectionStrings__DefaultConnection=...
Jwt__Key=...
```

Generate a strong key with `openssl rand -base64 64`.

### Run

```bash
dotnet restore
dotnet ef database update   # applies migrations, if the project uses them
dotnet run
```

Open the Swagger UI at `/swagger` and use **Authorize** with `Bearer <token>` to call protected endpoints.

## Authentication

1. `POST /api/Auth/register` or `POST /api/Auth/login` returns a JWT (valid for 7 days).
2. Send it on every protected request: `Authorization: Bearer <token>`.
3. The token carries the user id, email, role and phone number claims.

Registration rules:

- `Role` must be `Student` or `Teacher`.
- The phone number must start with the Qatar code `+974`.
- Email and phone number must be unique.
- A teacher registration automatically creates a default teacher profile.

## API reference

### Auth: `/api/Auth`

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/register` | Public | Create a student or teacher account |
| POST | `/login` | Public | Log in and receive a JWT |
| GET | `/profile` | Any user | Current user (plus teacher profile for teachers) |
| PUT | `/profile` | Any user | Update name, phone and teacher bio, subjects and hourly rate |
| POST | `/change-password` | Any user | Change password (requires the current password) |
| POST | `/logout` | Any user | Stateless logout, so the client discards the token |

### Student: `/api/Student`

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/courses` | Public | Browse active courses. Query: `subject`, `teacherId`, `search`, `page`, `pageSize` |
| GET | `/courses/{id}` | Public | Course details |
| GET | `/subjects` | Public | Distinct subjects that have active courses |
| GET | `/teachers/{id}` | Public | Public teacher profile |
| POST | `/bookings` | Student | Book a course session (starts as `Pending`) |
| GET | `/bookings` | Student | My bookings. Query: `status`, `page`, `pageSize` |
| PUT | `/bookings/{id}/cancel` | Student | Cancel a pending or confirmed booking |

### Teacher: `/api/Teacher` (role `Teacher` required)

| Tab | Method | Endpoint | Description |
|---|---|---|---|
| Home | GET | `/dashboard` | Greeting, pending requests, earnings, completed classes, rating, upcoming sessions |
| Bookings | GET | `/bookings` | All bookings, newest first. Query: `status`, `page`, `pageSize` |
| Bookings | GET | `/bookings/counts` | Count per status, for the filter chips |
| Bookings | GET | `/bookings/upcoming` | Confirmed future sessions, soonest first |
| Bookings | GET | `/bookings/{id}` | Booking details |
| Bookings | PUT | `/bookings/{id}/status` | Accept or reject a pending booking (`{ "accept": true }`) |
| Bookings | PUT | `/bookings/{id}/complete` | Mark a finished session as completed |
| Availability | GET | `/availability` | My slots, with an `isBooked` flag |
| Availability | POST | `/availability` | Add a recurring (weekly) or one-off slot |
| Availability | DELETE | `/availability/{id}` | Remove a slot |
| Account | GET | `/profile` | Teacher profile |
| Account | PUT | `/profile` | Update bio, qualifications, rate, subjects and languages |
| Account | POST | `/courses` | Create a course |
| Account | GET | `/courses` | My courses. Query: `isActive` |
| Account | GET | `/courses/{id}` | Course details |
| Account | PUT | `/courses/{id}` | Update a course (`isActive: false` hides it from students) |
| Account | DELETE | `/courses/{id}` | Deactivate a course (soft delete) |

## Business rules

**Booking lifecycle**

```
Pending ──accept──▶ Confirmed ──complete──▶ Completed
   │                    │
   └──reject / student cancel──▶ Cancelled
```

- A student can only book a future time that falls fully inside one of the teacher's availability slots.
- The booking is rejected if the teacher or the student already has an overlapping pending or confirmed session.
- Sessions can't run past midnight.
- If a course offers both modes, the student must choose `Online` or `InPerson`.
- A teacher can only accept a pending booking whose time hasn't passed, and can't confirm two overlapping sessions.
- A session can be marked completed only after it has finished.
- An availability slot can't be added if it overlaps an existing one, can't be in the past, and can't be deleted while it has a pending or confirmed booking.
- Deleting a course only deactivates it, so existing bookings stay valid.
- All times use Qatar time (UTC+3).

## Roadmap

- Student app endpoints for reviews and ratings
- Push and in-app notifications
- Payment gateway integration

## License

Add a license before accepting contributions (for example MIT).