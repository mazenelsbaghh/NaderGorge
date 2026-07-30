import { Pool, type PoolClient } from 'pg';
import crypto from 'crypto';
import dotenv from 'dotenv';
import { databaseUrl } from '../config/database.js';

dotenv.config();

function isLeapYear(year: number): boolean {
  return (year % 4 === 0 && year % 100 !== 0) || (year % 400 === 0);
}

export interface BirthdaySweepResult {
  scannedCount: number;
  congratulatedCount: number;
}

interface BirthdayStudent {
  id: string;
  fullName: string;
  phoneNumber: string;
  dateOfBirth: Date | string;
}

const IN_APP_NOTIFICATION_CHANNEL = 0;
const SENT_NOTIFICATION_STATUS = 1;

function cairoDateParts(now: Date) {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Africa/Cairo',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(now);
  const value = (type: Intl.DateTimeFormatPartTypes) =>
    Number(parts.find((part) => part.type === type)?.value);
  return {
    year: value('year'),
    month: value('month'),
    day: value('day'),
  };
}

function birthdayNotificationId(studentId: string, dateKey: string): string {
  const bytes = crypto
    .createHash('sha256')
    .update(`massar:birthday:${studentId}:${dateKey}`)
    .digest()
    .subarray(0, 16);
  bytes[6] = (bytes[6]! & 0x0f) | 0x50;
  bytes[8] = (bytes[8]! & 0x3f) | 0x80;
  const hex = bytes.toString('hex');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
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

async function loadActiveStudents(client: PoolClient): Promise<BirthdayStudent[]> {
  const result = await client.query<BirthdayStudent>(`
    SELECT u."Id" AS "id", u."FullName" AS "fullName",
           u."PhoneNumber" AS "phoneNumber", sp."DateOfBirth" AS "dateOfBirth"
    FROM "users" u
    JOIN "student_profiles" sp ON u."Id" = sp."UserId"
    WHERE u."IsActive" = true
      AND EXISTS (
        SELECT 1
        FROM "user_roles" ur
        JOIN "roles" r ON ur."RoleId" = r."Id"
        WHERE ur."UserId" = u."Id" AND r."Name" = 'Student'
      );
  `);
  return result.rows;
}

function isBirthdayOnCairoDate(
  dateOfBirth: Date | string,
  cairoDate: { year: number; month: number; day: number },
): boolean {
  const birthDate = new Date(dateOfBirth);
  const matchesToday =
    birthDate.getUTCMonth() + 1 === cairoDate.month &&
    birthDate.getUTCDate() === cairoDate.day;
  if (matchesToday) return true;
  return (
    cairoDate.month === 3 &&
    cairoDate.day === 1 &&
    !isLeapYear(cairoDate.year) &&
    birthDate.getUTCMonth() + 1 === 2 &&
    birthDate.getUTCDate() === 29
  );
}

async function createBirthdayNotification(
  client: PoolClient,
  student: BirthdayStudent,
  dateKey: string,
): Promise<boolean> {
  const notificationId = birthdayNotificationId(student.id, dateKey);
  const title = 'عيد ميلاد سعيد! 🎉';
  const body = `النهارده يوم مميز لأن المنصة بتحتفل بيك يا ${student.fullName}! كل سنة وأنت طيب، ونتمنى لك سنة جديدة مليانة نجاح وفرحة وتحقيق لكل أحلامك. 🎂✨`;
  const inserted = await client.query(`
    INSERT INTO "notification_events" ("Id", "UserId", "ChannelType", "Title", "Body", "Status", "CreatedAt")
    VALUES ($1, $2, $3, $4, $5, $6, NOW())
    ON CONFLICT ("Id") DO NOTHING
  `, [
    notificationId,
    student.id,
    IN_APP_NOTIFICATION_CHANNEL,
    title,
    body,
    SENT_NOTIFICATION_STATUS,
  ]);
  return inserted.rowCount === 1;
}

export async function runBirthdaySweep(
  database: Pool,
  now = new Date(),
): Promise<BirthdaySweepResult> {
  console.log('[BirthdayScript] Starting birthday congratulator sweep...');
  const cairoDate = cairoDateParts(now);
  const dateKey = `${cairoDate.year}-${String(cairoDate.month).padStart(2, '0')}-${String(cairoDate.day).padStart(2, '0')}`;

  console.log(`[BirthdayScript] Target Cairo Date: ${dateKey}`);

  const client = await database.connect();
  try {
    const students = await loadActiveStudents(client);
    console.log(`[BirthdayScript] Scanned ${students.length} active students.`);

    let congratulatedCount = 0;

    for (const student of students) {
      if (isBirthdayOnCairoDate(student.dateOfBirth, cairoDate)) {
        console.log(`[BirthdayScript] Found birthday matching: ${student.fullName} (${student.phoneNumber}) - DoB: ${student.dateOfBirth}`);
        if (!await createBirthdayNotification(client, student, dateKey)) {
          console.log(`[BirthdayScript] Birthday already recorded for ${student.id} on ${dateKey}.`);
          continue;
        }
        console.log(`[BirthdayScript] Created birthday notification for ${student.fullName}`);

        try {
          await sendWhatsAppMessage(student.phoneNumber, student.fullName);
        } catch (waErr) {
          console.error(`[BirthdayScript] Failed sending WhatsApp for student ${student.fullName}:`, waErr);
        }

        congratulatedCount++;
      }
    }

    console.log(`[BirthdayScript] Sweep completed. Congratulated ${congratulatedCount} students.`);
    return { scannedCount: students.length, congratulatedCount };
  } finally {
    client.release();
  }
}

async function runStandalone() {
  const database = new Pool({ connectionString: databaseUrl() });
  try {
    const overrideDate = process.env.OVERRIDE_DATE
      ? new Date(process.env.OVERRIDE_DATE)
      : new Date();
    await runBirthdaySweep(database, overrideDate);
  } finally {
    await database.end();
  }
}

if (process.argv[1] && import.meta.url === new URL(process.argv[1], 'file:').href) {
  runStandalone().catch((error) => {
    console.error('[BirthdayScript] Fatal execution error:', error);
    process.exitCode = 1;
  });
}
