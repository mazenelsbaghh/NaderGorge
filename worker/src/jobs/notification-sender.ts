import { Job } from 'bullmq';
import { maskId } from '../logging.js';

export async function processNotificationJob(job: Job) {
    console.log(`[NotificationSender] Processing job ${job.id} of name ${job.name}`);
    
    const data = job.data;
    const { StudentId, Severity, WarningId } = data;
    
    // Simulating call to Twilio or SMS Gateway
    console.log(`[NotificationSender] Sending ${Severity} SMS to student ${maskId(StudentId)}.`);
    
    // Fake latency
    await new Promise(resolve => setTimeout(resolve, 500));
    
    console.log(`[NotificationSender] SMS sent successfully for warning ${maskId(WarningId)}.`);
    return { success: true, deliveredAt: new Date().toISOString() };
}
