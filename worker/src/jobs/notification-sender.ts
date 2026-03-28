import { Job } from 'bullmq';

export async function processNotificationJob(job: Job) {
    console.log(`[NotificationSender] Processing job ${job.id} of name ${job.name}`);
    
    const data = job.data;
    const { StudentId, Severity, Message, WarningId } = data;
    
    // Simulating call to Twilio or SMS Gateway
    console.log(`[NotificationSender] Sending SMS to StudentId ${StudentId}...`);
    console.log(`[NotificationSender] Content: [${Severity}] ${Message}`);
    
    // Fake latency
    await new Promise(resolve => setTimeout(resolve, 500));
    
    console.log(`[NotificationSender] SMS Sent Successfully for WarningId ${WarningId}`);
    return { success: true, deliveredAt: new Date().toISOString() };
}
