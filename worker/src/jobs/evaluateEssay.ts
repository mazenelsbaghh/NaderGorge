import { config } from 'dotenv';
import { GoogleGenAI } from '@google/genai';
import { Job } from 'bullmq';

config();

const apiKeyStr = process.env.GEMINI_API_KEY || '';
const ai = new GoogleGenAI(apiKeyStr ? { apiKey: apiKeyStr } : {});
const API_URL = process.env.BACKEND_API_URL || 'http://localhost:5245/api/v1'; // Standard backend URL used alongside AI Chaptering
const API_CALLBACK_SECRET = process.env.AI_CALLBACK_SECRET || process.env.API_CALLBACK_SECRET;

export interface EvaluateEssayJobData {
  essaySubmissionId: string;
  questionId: string;
  studentId: string;
  answerText: string;
  expectedAnswer?: string;
}

export async function processEvaluateEssayJob(job: Job<EvaluateEssayJobData>) {
  const { essaySubmissionId, answerText, expectedAnswer } = job.data;
  
  await job.updateProgress({ percentage: 10, stage: 'بنحلل إجابتك...' });
  
  console.log(`[EvaluateEssay] Starting evaluation for essay ${essaySubmissionId}`);

  try {
    const prompt = `
You are a friendly Egyptian Arabic teacher who speaks in Egyptian colloquial Arabic (العامية المصرية).
The student has submitted an answer to an essay question.

Teacher's Expected Answer / Key concepts:
${expectedAnswer || "مفيش إجابة نموذجية متوفرة، قيّم الإجابة على أساس المنطق العام."}

Student Answer:
${answerText}

Task:
1. Determine if the student's answer is correct based on the expected answer.
2. Provide a short 1-2 sentence feedback in EGYPTIAN COLLOQUIAL ARABIC (العامية المصرية). Use a warm, encouraging tone like a friend talking. For example: "برافو عليك، إجابتك مظبوطة وجبت النقطة الأساسية." or "إجابتك ناقصة شوية، كان لازم تغطي كل الجوانب المطلوبة."
IMPORTANT: You MUST NOT write the correct answer in your feedback. Simply tell them if their logic is correct or incorrect and briefly why in general terms.

Return the result STRICTLY as a JSON object with this shape:
{
  "isCorrect": boolean,
  "feedback": "string"
}
Do not return any markdown code blocks, just raw JSON.`;

    const response = await ai.models.generateContent({
      model: 'gemini-2.5-flash',
      contents: prompt,
      config: {
        responseMimeType: "application/json",
      }
    });

    const responseText = response.text || "{}";
    await job.updateProgress({ percentage: 60, stage: 'بنجهّز النتيجة...' });

    console.log(`[EvaluateEssay] Gemini response received (${responseText.length} chars).`);

    let parsed: { isCorrect: boolean, feedback: string };
    try {
      parsed = JSON.parse(responseText);
    } catch (e) {
      console.error(`[EvaluateEssay] JSON parse error:`, e);
      throw new Error("Failed to parse Gemini response");
    }

    // Map true/false to 1/0 for the webhook score
    const safeScore = parsed.isCorrect ? 1 : 0;
    
    // Webhook callback to C# API
    await job.updateProgress({ percentage: 80, stage: 'بنبعت النتيجة...' });
    
    const webhookResponse = await fetch(`${API_URL}/internal/callbacks/essay-graded`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Internal-Token': API_CALLBACK_SECRET || ''
      },
      body: JSON.stringify({
        essaySubmissionId,
        aiScore: safeScore,
        aiFeedback: parsed.feedback
      })
    });
    
    if (!webhookResponse.ok) {
       const errBody = await webhookResponse.text();
       throw new Error(`Webhook failed with status ${webhookResponse.status}: ${errBody}`);
    }

    await job.updateProgress({ percentage: 100, stage: 'خلصنا التقييم! ✅' });
    console.log(`[EvaluateEssay] Completed successfully for ${essaySubmissionId}`);
    
    return { success: true, score: safeScore, feedback: parsed.feedback };

  } catch (error: any) {
    console.error(`[EvaluateEssay] Failed:`, error.message);
    throw error;
  }
}
