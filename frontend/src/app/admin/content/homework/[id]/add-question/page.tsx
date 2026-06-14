import AddHomeworkQuestionPageClient from './AddHomeworkQuestionPageClient';

export default function Page({ params }: { params: { id: string } }) {
  return <AddHomeworkQuestionPageClient params={params} />;
}
