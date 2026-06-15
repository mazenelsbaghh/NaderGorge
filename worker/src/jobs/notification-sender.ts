import { Job } from 'bullmq';
import { maskId } from '../logging.js';
import { Pool } from 'pg';
import dotenv from 'dotenv';

dotenv.config();

let dbUrl = process.env.DATABASE_URL;
if (!dbUrl && process.env.DB_CONNECTION_STRING) {
  dbUrl = process.env.DB_CONNECTION_STRING;
}
dbUrl = dbUrl || 'postgresql://postgres:postgres@localhost:5432/nadergorge?schema=public';

const pool = new Pool({
  connectionString: dbUrl
});

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
                text
            }
        })
    });

    if (!res.ok) {
        const errText = await res.text();
        throw new Error(`Evolution API send text failed: status=${res.status}, body=${errText}`);
    }
}

export async function processNotificationJob(job: Job) {
    console.log(`[NotificationSender] Processing job ${job.id} of name ${job.name}`);
    
    // Explicitly fail if provider settings are absent
    const baseUrl = process.env.EVOLUTION_API_BASE_URL;
    const apiKey = process.env.EVOLUTION_API_KEY;
    if (!baseUrl || !apiKey) {
        throw new Error('Notification provider settings are absent. EVOLUTION_API_BASE_URL and EVOLUTION_API_KEY must be configured.');
    }

    const data = job.data;
    
    if (job.name === 'chat-mention') {
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

    const { StudentId, Severity, WarningId, Message } = data;
    if (StudentId) {
        const studentRes = await pool.query('SELECT "FullName", "PhoneNumber" FROM "users" WHERE "Id" = $1', [StudentId]);
        if (studentRes.rows.length === 0) {
            throw new Error(`Student with ID ${StudentId} not found.`);
        }

        const { FullName, PhoneNumber } = studentRes.rows[0];
        if (!PhoneNumber) {
            throw new Error(`Student ${FullName} does not have a phone number configured.`);
        }

        const text = `تنبيه أكاديمي من أكاديمية الأستاذ نادر جورج: تم تسجيل تنبيه جديد لك (${Severity || 'تنبيه'}). السبب: ${Message || 'غير محدد'}`;
        console.log(`[NotificationSender] Sending ${Severity || 'Info'} SMS to student ${FullName} (${maskId(StudentId)}).`);
        await sendWhatsAppMessage(PhoneNumber, text);
        console.log(`[NotificationSender] SMS sent successfully for warning ${maskId(WarningId)}.`);
        
        return { success: true, deliveredAt: new Date().toISOString() };
    }

    throw new Error('Unsupported notification job payload.');
}
