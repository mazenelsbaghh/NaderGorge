import { Job } from 'bullmq';
import { maskId } from '../logging.js';

export async function processNotificationJob(job: Job) {
    console.log(`[NotificationSender] Processing job ${job.id} of name ${job.name}`);
    
    const data = job.data;
    
    if (job.name === 'chat-mention') {
        const { targetUserId, senderName } = data;
        console.log(`[NotificationSender] Chat mention alert for user ${maskId(targetUserId)} by ${senderName}.`);
        await new Promise(resolve => setTimeout(resolve, 300));
        return { success: true, type: 'ChatMention', deliveredAt: new Date().toISOString() };
    }

    const { StudentId, Severity, WarningId } = data;
    if (StudentId) {
        console.log(`[NotificationSender] Sending ${Severity || 'Info'} SMS to student ${maskId(StudentId)}.`);
        await new Promise(resolve => setTimeout(resolve, 500));
        console.log(`[NotificationSender] SMS sent successfully for warning ${maskId(WarningId)}.`);
    } else {
        console.log(`[NotificationSender] Generic notification processed successfully.`);
    }

    return { success: true, deliveredAt: new Date().toISOString() };
}
