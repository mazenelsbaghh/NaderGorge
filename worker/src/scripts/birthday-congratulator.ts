import { Pool } from 'pg';
import crypto from 'crypto';
import dotenv from 'dotenv';

dotenv.config();

function parseConnectionString(connStr: string): string {
  const parts = connStr.split(';');
  const dict: Record<string, string> = {};
  for (const part of parts) {
    const [key, val] = part.split('=');
    if (key && val) {
      dict[key.trim().toLowerCase()] = val.trim();
    }
  }
  const host = dict['host'] || 'localhost';
  const port = dict['port'] || '5432';
  const database = dict['database'] || 'nadergorge';
  const username = dict['username'] || dict['user'] || 'postgres';
  const password = dict['password'] || 'postgres';
  return `postgresql://${username}:${password}@${host}:${port}/${database}`;
}

let dbUrl = process.env.DATABASE_URL;
if (!dbUrl && process.env.DB_CONNECTION_STRING) {
  if (process.env.DB_CONNECTION_STRING.includes('=')) {
    dbUrl = parseConnectionString(process.env.DB_CONNECTION_STRING);
  } else {
    dbUrl = process.env.DB_CONNECTION_STRING;
  }
}
dbUrl = dbUrl || 'postgresql://postgres:postgres@localhost:5435/nadergorge?schema=public';

const pool = new Pool({
  connectionString: dbUrl
});

function isLeapYear(year: number): boolean {
  return (year % 4 === 0 && year % 100 !== 0) || (year % 400 === 0);
}

async function sendWhatsAppMessage(phone: string, name: string): Promise<boolean> {
  const baseUrl = process.env.EVOLUTION_API_BASE_URL;
  const apiKey = process.env.EVOLUTION_API_KEY;
  const instance = process.env.EVOLUTION_API_INSTANCE || 'Nader';

  if (!baseUrl || !apiKey) {
    console.log('[BirthdayScript] Evolution API not configured. Skipping WhatsApp message.');
    return false;
  }

  // Normalize Egyptian number (01X...) to international (201X...)
  let internationalNumber = phone;
  if (phone.startsWith('0')) {
    internationalNumber = '20' + phone.substring(1);
  }

  const url = `${baseUrl}/message/sendText/${instance}`;
  const greetingText = `كل عام وأنت بخير يا ${name}! 🎉\nبمناسبة عيد ميلادك، تتمنى لك أسرة أكاديمية الأستاذ نادر جورج عاماً دراسياً مليئاً بالنجاح والتفوق. 🎂✨`;

  try {
    const res = await fetch(url, {
      method: 'POST',
      headers: {
        'apikey': apiKey,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        number: internationalNumber,
        options: {
          delay: 1200,
          presence: 'composing'
        },
        textMessage: {
          text: greetingText
        }
      })
    });

    if (!res.ok) {
      const errText = await res.text();
      console.error(`[BirthdayScript] WhatsApp failed for ${internationalNumber}: status=${res.status}, body=${errText}`);
      return false;
    }

    console.log(`[BirthdayScript] WhatsApp sent successfully to ${internationalNumber}`);
    return true;
  } catch (err) {
    console.error(`[BirthdayScript] WhatsApp API request failed for ${internationalNumber}:`, err);
    return false;
  }
}

async function run() {
  console.log('[BirthdayScript] Starting birthday congratulator sweep...');
  
  // Calculate today's date in Africa/Cairo timezone
  let egyptDate: Date;
  if (process.env.OVERRIDE_DATE) {
    egyptDate = new Date(process.env.OVERRIDE_DATE);
  } else {
    const egyptTimeStr = new Date().toLocaleString("en-US", { timeZone: "Africa/Cairo" });
    egyptDate = new Date(egyptTimeStr);
  }
  const todayMonth = egyptDate.getMonth() + 1; // 1-12
  const todayDay = egyptDate.getDate(); // 1-31
  const todayYear = egyptDate.getFullYear();

  console.log(`[BirthdayScript] Target Cairo Date: ${todayYear}-${todayMonth}-${todayDay}`);

  const includeLeapDay = todayMonth === 3 && todayDay === 1 && !isLeapYear(todayYear);
  if (includeLeapDay) {
    console.log('[BirthdayScript] Leap-year edge case: today is March 1st on a non-leap year. Including Feb 29 birthdays.');
  }

  const client = await pool.connect();
  try {
    await client.query('BEGIN');

    // Query active students
    const query = `
      SELECT u."Id" AS "id", u."FullName" AS "fullName", u."PhoneNumber" AS "phoneNumber", sp."DateOfBirth" AS "dateOfBirth"
      FROM "users" u
      JOIN "student_profiles" sp ON u."Id" = sp."UserId"
      WHERE u."IsActive" = true
        AND u."Id" IN (
          SELECT ur."UserId" 
          FROM "user_roles" ur 
          JOIN "roles" r ON ur."RoleId" = r."Id" 
          WHERE r."Name" = 'Student'
        );
    `;

    const res = await client.query(query);
    const students = res.rows;
    console.log(`[BirthdayScript] Scanned ${students.length} active students.`);

    let congratulatedCount = 0;

    for (const student of students) {
      if (!student.dateOfBirth) continue;
      
      const dob = new Date(student.dateOfBirth);
      // Since DateOfBirth is UTC, evaluate its month/day
      const birthMonth = dob.getUTCMonth() + 1;
      const birthDay = dob.getUTCDate();

      let isBirthday = (birthMonth === todayMonth && birthDay === todayDay);
      
      // If March 1st on a non-leap year, include Feb 29
      if (!isBirthday && includeLeapDay && birthMonth === 2 && birthDay === 29) {
        isBirthday = true;
      }

      if (isBirthday) {
        console.log(`[BirthdayScript] Found birthday matching: ${student.fullName} (${student.phoneNumber}) - DoB: ${student.dateOfBirth}`);
        
        // 1. Create In-App Notification Event
        const notificationId = crypto.randomUUID();
        const title = 'عيد ميلاد سعيد! 🎉';
        const body = `كل عام وأنت بخير يا ${student.fullName}! بمناسبة عيد ميلادك، تتمنى لك أسرة أكاديمية الأستاذ نادر جورج عاماً دراسياً مليئاً بالنجاح والتفوق. 🎂✨`;
        
        await client.query(`
          INSERT INTO "notification_events" ("Id", "UserId", "ChannelType", "Title", "Body", "Status", "CreatedAt")
          VALUES ($1, $2, $3, $4, $5, $6, NOW())
        `, [notificationId, student.id, 0, title, body, 1]); // ChannelType 0 = InApp, Status 1 = Sent

        console.log(`[BirthdayScript] Created In-App NotificationEvent: ${notificationId} for ${student.fullName}`);

        // 2. Dispatch WhatsApp Congratulations via Evolution API (Fire-and-forget/non-blocking)
        try {
          await sendWhatsAppMessage(student.phoneNumber, student.fullName);
        } catch (waErr) {
          console.error(`[BirthdayScript] Failed sending WhatsApp for student ${student.fullName}:`, waErr);
        }

        congratulatedCount++;
      }
    }

    await client.query('COMMIT');
    console.log(`[BirthdayScript] Sweep completed. Congratulated ${congratulatedCount} students.`);
  } catch (error) {
    await client.query('ROLLBACK');
    console.error('[BirthdayScript] Birthday congratulator script failed:', error);
  } finally {
    client.release();
    await pool.end();
  }
}

run().catch(err => {
  console.error('[BirthdayScript] Fatal execution error:', err);
  process.exit(1);
});
