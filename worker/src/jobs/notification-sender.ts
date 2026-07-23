import { Job } from 'bullmq';
import { maskId } from '../logging.js';
import { Pool } from 'pg';
import dotenv from 'dotenv';
import { getApps, initializeApp, cert, applicationDefault } from 'firebase-admin/app';
import { getMessaging } from 'firebase-admin/messaging';
import fs from 'node:fs';
import { fetchWithTimeout } from '../services/workerFetch.js';
import { randomUUID } from 'node:crypto';

dotenv.config();

let dbUrl = process.env.DATABASE_URL;
if (!dbUrl && process.env.DB_CONNECTION_STRING) {
  dbUrl = process.env.DB_CONNECTION_STRING;
}
dbUrl = dbUrl || 'postgresql://postgres:postgres@localhost:5432/nadergorge?schema=public';

const pool = new Pool({
  connectionString: dbUrl
});

// Initialize Firebase Admin SDK
if (getApps().length === 0) {
  if (process.env.FIREBASE_SERVICE_ACCOUNT_JSON) {
    try {
      const serviceAccount = JSON.parse(process.env.FIREBASE_SERVICE_ACCOUNT_JSON);
      initializeApp({
        credential: cert(serviceAccount)
      });
    } catch (e) {
      console.error('Failed to parse FIREBASE_SERVICE_ACCOUNT_JSON, using applicationDefault:', e);
      initializeApp({
        credential: applicationDefault()
      });
    }
  } else if (process.env.FIREBASE_APPLICATION_CREDENTIALS && fs.existsSync(process.env.FIREBASE_APPLICATION_CREDENTIALS)) {
    const serviceAccount = JSON.parse(fs.readFileSync(process.env.FIREBASE_APPLICATION_CREDENTIALS, 'utf8'));
    initializeApp({
      credential: cert(serviceAccount)
    });
  } else if (process.env.GOOGLE_APPLICATION_CREDENTIALS && fs.existsSync(process.env.GOOGLE_APPLICATION_CREDENTIALS)) {
    initializeApp({
      credential: applicationDefault()
    });
  } else {
    // For testing/CI without credentials, use a dummy project ID so it won't throw
    initializeApp({
      projectId: 'dummy-project-id'
    });
  }
}

// Exported wrapper to allow easy unit testing and mocking
export const firebaseMessaging = {
  sendEachForMulticast: async (message: any) => {
    return await getMessaging().sendEachForMulticast(message);
  }
};

async function sendWhatsAppMessage(phone: string, text: string) {
    const baseUrl = process.env.EVOLUTION_API_BASE_URL;
    const apiKey = process.env.EVOLUTION_API_KEY;
    const instance = process.env.EVOLUTION_API_INSTANCE || 'Nader';

    if (!baseUrl || !apiKey) {
        throw new Error('Evolution API credentials are not configured.');
    }

    let internationalNumber = phone;
    if (phone.startsWith('0')) {
        internationalNumber = '20' + phone.substring(1);
    }

    const url = `${baseUrl}/message/sendText/${instance}`;
    const res = await fetchWithTimeout(url, {
        method: 'POST',
        timeoutMs: 10_000,
        operation: 'evolution-whatsapp',
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
                text
            }
        })
    });

    if (!res.ok) {
        const errText = await res.text();
        throw new Error(`Evolution API send text failed: status=${res.status}, body=${errText}`);
    }
}

async function persistParentPushNotification(studentId: string, title: string, body: string) {
  await pool.query(
    `INSERT INTO "notification_events" ("Id", "UserId", "ChannelType", "Title", "Body", "Status", "CreatedAt")
     SELECT $1, $2, 0, $3, $4, 1, NOW()
     WHERE NOT EXISTS (
       SELECT 1
       FROM "notification_events"
       WHERE "UserId" = $2
         AND "Title" = $3
         AND "Body" = $4
         AND "CreatedAt" > NOW() - INTERVAL '30 minutes'
     )`,
    [randomUUID(), studentId, title, body]
  );
}

export async function processParentPushNotification(studentId: string, title: string, body: string, category: string, persistInApp = true) {
  // Load FCM tokens from ParentDeviceTokens linked to StudentId.
  const res = await pool.query(
    `SELECT DISTINCT pdt."DeviceToken"
     FROM "ParentDeviceTokens" pdt
     LEFT JOIN student_profiles sp ON sp."Id" = pdt."StudentId"
     WHERE pdt."StudentId" = $1 OR sp."UserId" = $1`,
    [studentId]
  );
  
  const tokens = res.rows.map((row: any) => row.DeviceToken).filter(Boolean);
  
  if (tokens.length === 0) {
    console.log(`[NotificationSender] No parent device tokens found for studentId ${studentId}`);
    return { success: true, reason: 'no_tokens', tokensCount: 0 };
  }

  if (persistInApp) {
    await persistParentPushNotification(studentId, title, body);
  }

  const message = {
    notification: {
      title,
      body
    },
    data: {
      studentId,
      category
    },
    tokens
  };

  try {
    const response = await firebaseMessaging.sendEachForMulticast(message);
    console.log(`[NotificationSender] Sent push notifications. Success: ${response.successCount}, Failure: ${response.failureCount}`);
    return {
      success: true,
      tokensCount: tokens.length,
      successCount: response.successCount,
      failureCount: response.failureCount
    };
  } catch (error: any) {
    console.error('[NotificationSender] Error sending multicast message:', error);
    throw error;
  }
}

export async function processNotificationJob(job: Job) {
    console.log(`[NotificationSender] Processing job ${job.id} of name ${job.name}`);
    
    const data = job.data;
    
    if (job.name === 'chat-mention') {
        const baseUrl = process.env.EVOLUTION_API_BASE_URL;
        const apiKey = process.env.EVOLUTION_API_KEY;
        if (!baseUrl || !apiKey) {
            throw new Error('Notification provider settings are absent. EVOLUTION_API_BASE_URL and EVOLUTION_API_KEY must be configured.');
        }

        const { targetUserId, senderName } = data;
        if (!targetUserId) {
            throw new Error('targetUserId is required for chat-mention jobs.');
        }

        const userRes = await pool.query('SELECT "FullName", "PhoneNumber" FROM "users" WHERE "Id" = $1', [targetUserId]);
        if (userRes.rows.length === 0) {
            throw new Error(`User with ID ${targetUserId} not found.`);
        }

        const { FullName, PhoneNumber } = userRes.rows[0];
        if (!PhoneNumber) {
            throw new Error(`User ${FullName} does not have a phone number configured.`);
        }

        const messageText = `تنبيه: تم ذكرك في المحادثة بواسطة ${senderName || 'مستخدم آخر'}.`;
        console.log(`[NotificationSender] Sending chat mention to ${FullName} (${maskId(targetUserId)})`);
        await sendWhatsAppMessage(PhoneNumber, messageText);

        return { success: true, type: 'ChatMention', deliveredAt: new Date().toISOString() };
    }

    if (job.name === 'parent-push') {
        const { StudentId, studentId, Title, title, Body, body, Category, category, HomeworkTitle, homeworkTitle } = data;
        const actualStudentId = StudentId || studentId;
        const actualCategory = Category || category || 'General';
        const actualTitle = Title || title || defaultParentPushTitle(actualCategory);
        const actualBody = Body || body || defaultParentPushBody(actualCategory, HomeworkTitle || homeworkTitle);

        if (!actualStudentId || !actualTitle || !actualBody) {
            throw new Error('StudentId, Title, and Body are required for parent-push notification jobs.');
        }

        console.log(`[NotificationSender] Processing parent-push job for student ${actualStudentId}`);
        const res = await processParentPushNotification(actualStudentId, actualTitle, actualBody, actualCategory);
        return { type: 'ParentPush', ...res, deliveredAt: new Date().toISOString() };
    }

    const { StudentId, Severity, WarningId, Message, ParentPush, Category } = data;
    if (StudentId) {
        let smsSent = false;
        try {
            const baseUrl = process.env.EVOLUTION_API_BASE_URL;
            const apiKey = process.env.EVOLUTION_API_KEY;
            if (!baseUrl || !apiKey) {
                console.warn('[NotificationSender] WhatsApp settings missing, skipping SMS for warning.');
            } else {
                const studentRes = await pool.query('SELECT "FullName", "PhoneNumber" FROM "users" WHERE "Id" = $1', [StudentId]);
                if (studentRes.rows.length > 0) {
                    const { FullName, PhoneNumber } = studentRes.rows[0];
                    if (PhoneNumber) {
                        const text = `تنبيه أكاديمي من أكاديمية الأستاذ نادر جورج: تم تسجيل تنبيه جديد لك (${Severity || 'تنبيه'}). السبب: ${Message || 'غير محدد'}`;
                        console.log(`[NotificationSender] Sending ${Severity || 'Info'} SMS to student ${FullName} (${maskId(StudentId)}).`);
                        await sendWhatsAppMessage(PhoneNumber, text);
                        console.log(`[NotificationSender] SMS sent successfully for warning ${maskId(WarningId)}.`);
                        smsSent = true;
                    }
                }
            }
        } catch (smsErr) {
            console.error('[NotificationSender] Failed to send WhatsApp warning to student:', smsErr);
        }

        let parentPushSent = false;
        let parentPushResult: any = null;
        if (WarningId || ParentPush) {
            const title = `تنبيه أكاديمي جديد`;
            const body = `تم تسجيل تنبيه جديد لولدكم: ${Message || 'غير محدد'}`;
            const actualCategory = Category || 'Warning';
            parentPushResult = await processParentPushNotification(StudentId, title, body, actualCategory, false);
            parentPushSent = true;
        }

        return { 
            success: true, 
            smsSent, 
            parentPushSent, 
            parentPushResult,
            deliveredAt: new Date().toISOString() 
        };
    }

    throw new Error('Unsupported notification job payload.');
}

function defaultParentPushTitle(category: string) {
    switch ((category || '').toLowerCase()) {
        case 'homework':
            return 'تسليم واجب جديد';
        case 'exam':
            return 'حل اختبار جديد';
        case 'purchase':
            return 'شراء جديد للطالب';
        default:
            return 'تنبيه جديد لولي الأمر';
    }
}

function defaultParentPushBody(category: string, itemTitle?: string) {
    const safeTitle = itemTitle ? `: ${itemTitle}` : '';
    switch ((category || '').toLowerCase()) {
        case 'homework':
            return `تم تسليم واجب جديد${safeTitle}.`;
        case 'exam':
            return `تم حل اختبار جديد${safeTitle}.`;
        case 'purchase':
            return `تم تفعيل محتوى جديد للطالب${safeTitle}.`;
        default:
            return 'يوجد تحديث جديد في متابعة الطالب.';
    }
}
