-- ============================================================
--  StitchFlow Database Schema
--  Database: PostgreSQL 15+
--  Run this file to set up the full database
-- ============================================================

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ──────────────────────────────────────────
-- 1. USERS
-- ──────────────────────────────────────────
CREATE TABLE users (
    id            UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name          VARCHAR(150)        NOT NULL,
    email         VARCHAR(255)        NOT NULL UNIQUE,
    phone         VARCHAR(20),
    password_hash TEXT                NOT NULL,
    role          VARCHAR(20)         NOT NULL CHECK (role IN ('tailor', 'customer')),
    is_active     BOOLEAN             NOT NULL DEFAULT TRUE,
    created_at    TIMESTAMPTZ         NOT NULL DEFAULT NOW(),
    updated_at    TIMESTAMPTZ         NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_users_role  ON users(role);

-- ──────────────────────────────────────────
-- 2. TAILOR PROFILES
-- ──────────────────────────────────────────
CREATE TABLE tailor_profiles (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id         UUID        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    shop_name       VARCHAR(200) NOT NULL,
    specialization  VARCHAR(50)  NOT NULL CHECK (specialization IN ('gents', 'ladies', 'kids', 'all')),
    address         TEXT,
    city            VARCHAR(100),
    logo_url        TEXT,
    shop_code       VARCHAR(20)  NOT NULL UNIQUE,   -- e.g. HASSAN-001 (customers use this to join)
    bio             TEXT,
    is_verified     BOOLEAN      NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    UNIQUE(user_id)
);

CREATE INDEX idx_tailor_profiles_user_id ON tailor_profiles(user_id);
CREATE INDEX idx_tailor_profiles_shop_code ON tailor_profiles(shop_code);

-- ──────────────────────────────────────────
-- 3. CUSTOMER ↔ TAILOR LINK
-- ──────────────────────────────────────────
CREATE TABLE customer_tailor_links (
    id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    customer_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    tailor_id   UUID NOT NULL REFERENCES tailor_profiles(id) ON DELETE CASCADE,
    linked_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(customer_id, tailor_id)
);

-- ──────────────────────────────────────────
-- 4. MEASUREMENTS
-- ──────────────────────────────────────────
CREATE TABLE measurements (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    customer_id         UUID           NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    tailor_id           UUID           NOT NULL REFERENCES tailor_profiles(id) ON DELETE CASCADE,
    neck                DECIMAL(5,2),
    chest               DECIMAL(5,2),
    waist               DECIMAL(5,2),
    shoulder            DECIMAL(5,2),
    sleeve              DECIMAL(5,2),
    length              DECIMAL(5,2),
    hip                 DECIMAL(5,2),
    thigh               DECIMAL(5,2),
    inseam              DECIMAL(5,2),
    notes               TEXT,
    reference_image_url TEXT,
    created_at          TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ    NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_measurements_customer_id ON measurements(customer_id);
CREATE INDEX idx_measurements_tailor_id   ON measurements(tailor_id);

-- ──────────────────────────────────────────
-- 5. DESIGNS
-- ──────────────────────────────────────────
CREATE TABLE designs (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tailor_id       UUID         NOT NULL REFERENCES tailor_profiles(id) ON DELETE CASCADE,
    name            VARCHAR(200) NOT NULL,
    description     TEXT,
    category        VARCHAR(20)  NOT NULL CHECK (category IN ('men', 'women', 'kids', 'unisex')),
    garment_type    VARCHAR(50)  NOT NULL,   -- Shalwar Kameez, Sherwani, Pant Coat, Suit, Kurta, etc.
    base_price      DECIMAL(10,2) NOT NULL DEFAULT 0,
    stitching_days  INT          NOT NULL DEFAULT 7,
    is_active       BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_designs_tailor_id    ON designs(tailor_id);
CREATE INDEX idx_designs_category     ON designs(category);
CREATE INDEX idx_designs_garment_type ON designs(garment_type);

-- Design images (one design can have multiple images)
CREATE TABLE design_images (
    id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    design_id   UUID NOT NULL REFERENCES designs(id) ON DELETE CASCADE,
    image_url   TEXT NOT NULL,
    is_primary  BOOLEAN NOT NULL DEFAULT FALSE,
    sort_order  INT     NOT NULL DEFAULT 0,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ──────────────────────────────────────────
-- 6. ORDERS
-- ──────────────────────────────────────────
CREATE TABLE orders (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    order_number        VARCHAR(20)   NOT NULL UNIQUE,  -- e.g. SF-1031
    customer_id         UUID          NOT NULL REFERENCES users(id),
    tailor_id           UUID          NOT NULL REFERENCES tailor_profiles(id),
    design_id           UUID          REFERENCES designs(id),
    measurement_id      UUID          REFERENCES measurements(id),
    garment_type        VARCHAR(50)   NOT NULL,
    cloth_image_url     TEXT,
    special_instructions TEXT,
    delivery_date       DATE,
    priority            VARCHAR(20)   NOT NULL DEFAULT 'normal' CHECK (priority IN ('normal', 'urgent', 'rush')),
    status              VARCHAR(30)   NOT NULL DEFAULT 'pending'
                            CHECK (status IN ('pending','in_progress','cutting','stitching','finishing','ready','delivered','cancelled')),
    tailor_note         TEXT,
    total_amount        DECIMAL(10,2) NOT NULL DEFAULT 0,
    advance_paid        DECIMAL(10,2) NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_orders_customer_id ON orders(customer_id);
CREATE INDEX idx_orders_tailor_id   ON orders(tailor_id);
CREATE INDEX idx_orders_status      ON orders(status);

-- Order status history (full audit trail)
CREATE TABLE order_status_history (
    id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    order_id    UUID        NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    old_status  VARCHAR(30),
    new_status  VARCHAR(30) NOT NULL,
    note        TEXT,
    changed_by  UUID        NOT NULL REFERENCES users(id),
    changed_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ──────────────────────────────────────────
-- 7. PAYMENTS
-- ──────────────────────────────────────────
CREATE TABLE payments (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    order_id        UUID          NOT NULL REFERENCES orders(id),
    amount          DECIMAL(10,2) NOT NULL,
    payment_method  VARCHAR(30)   NOT NULL CHECK (payment_method IN ('cash','bank_transfer','online','mobile_wallet')),
    status          VARCHAR(20)   NOT NULL DEFAULT 'pending' CHECK (status IN ('pending','paid','failed','refunded')),
    reference_number VARCHAR(100),
    notes           TEXT,
    payment_date    TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_payments_order_id ON payments(order_id);

-- ──────────────────────────────────────────
-- 8. NOTIFICATIONS
-- ──────────────────────────────────────────
CREATE TABLE notifications (
    id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id     UUID        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    order_id    UUID        REFERENCES orders(id) ON DELETE SET NULL,
    title       VARCHAR(200) NOT NULL,
    message     TEXT        NOT NULL,
    type        VARCHAR(30) NOT NULL CHECK (type IN ('order_update','payment','measurement','system','review')),
    is_read     BOOLEAN     NOT NULL DEFAULT FALSE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_notifications_user_id ON notifications(user_id);
CREATE INDEX idx_notifications_is_read ON notifications(is_read);

-- ──────────────────────────────────────────
-- 9. REVIEWS
-- ──────────────────────────────────────────
CREATE TABLE reviews (
    id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    order_id    UUID    NOT NULL REFERENCES orders(id),
    customer_id UUID    NOT NULL REFERENCES users(id),
    tailor_id   UUID    NOT NULL REFERENCES tailor_profiles(id),
    rating      INT     NOT NULL CHECK (rating BETWEEN 1 AND 5),
    comment     TEXT,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(order_id, customer_id)
);

CREATE INDEX idx_reviews_tailor_id ON reviews(tailor_id);

-- ──────────────────────────────────────────
-- 10. REFRESH TOKENS (for JWT auth)
-- ──────────────────────────────────────────
CREATE TABLE refresh_tokens (
    id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id     UUID        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token       TEXT        NOT NULL UNIQUE,
    expires_at  TIMESTAMPTZ NOT NULL,
    is_revoked  BOOLEAN     NOT NULL DEFAULT FALSE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ──────────────────────────────────────────
-- SEED DATA (Demo)
-- ──────────────────────────────────────────

-- Demo tailor user (password: Password123!)
INSERT INTO users (id, name, email, phone, password_hash, role)
VALUES (
    'a1b2c3d4-0000-0000-0000-000000000001',
    'Hassan Tariq',
    'hassan@stitchflow.com',
    '+92-300-1234567',
    '$2a$12$examplehashedpasswordforhassanhere1234567890abc',
    'tailor'
);

INSERT INTO tailor_profiles (user_id, shop_name, specialization, address, city, shop_code)
VALUES (
    'a1b2c3d4-0000-0000-0000-000000000001',
    'Hassan Tailor House',
    'all',
    'Main Market, Gulberg III',
    'Lahore',
    'HASSAN-001'
);

-- Demo customer user (password: Password123!)
INSERT INTO users (id, name, email, phone, password_hash, role)
VALUES (
    'a1b2c3d4-0000-0000-0000-000000000002',
    'Adnan Khan',
    'adnan@gmail.com',
    '+92-311-9876543',
    '$2a$12$examplehashedpasswordforadnanhere1234567890abc',
    'customer'
);
