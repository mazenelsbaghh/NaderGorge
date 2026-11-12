import Link from "next/link";

const id = "[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}";
const lessonPath = "/student/lessons(?:/" + id + "(?:\\?videoId=" + id + ")?)?";
export const studentLessonPath = new RegExp(`^${lessonPath}$`, "i");
const lessonPathInText = new RegExp(`(?<![\\w./])(${lessonPath})(?=$|[\\s.,!?،؛؟:<>"'()\\[\\]])`, "i");

export function StudentLessonLinks({ text, onClick }: { text: string; onClick?: () => void }) {
  return text.split(lessonPathInText).map((part, index) => (
    index % 2 === 0 ? part : (
      <Link key={index} href={part} prefetch={false} onClick={onClick} className="font-bold text-[var(--admin-primary)] underline">
        فتح الدرس على مسار
      </Link>
    )
  ));
}
