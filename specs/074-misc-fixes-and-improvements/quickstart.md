# Quickstart: Miscellaneous Fixes and Improvements

This guide details steps to quickly run, build, and verify the features and fixes.

## 1. Local Environment Config

In your `.env` file, specify the public application URL for correct QR code generation:

```env
NEXT_PUBLIC_APP_URL=https://nadergeorge.com
```

## 2. DB Migrations

To apply the database changes:

1. Create a migration in backend:
   ```bash
   make migrate-add NAME=AddSuspensionReasonToUser
   ```
2. Apply the migration:
   ```bash
   make migrate
   ```

## 3. Rate Limiting verification

Ensure that running standard tests or browsing the platform does not trigger `429 Too Many Requests` as frequently under standard usage.

## 4. UI Checks

1. **Sidebar Navigation**: Hover on the sidebar icons in the admin or student pages. It must expand and reveal text next to the icons.
2. **Balance Edit Button**: Check the student profile financials tab and confirm the edit balance button is aligned horizontally and sitting cleanly inside the card.
3. **Login Redirect**: Log in, then manually visit `/login` in your browser. Verify it redirects you automatically.
