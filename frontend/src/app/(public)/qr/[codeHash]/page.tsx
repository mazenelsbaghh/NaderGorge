import { QrRedeemClient } from "./QrRedeemClient";

export default async function QrRedeemPage({
  params,
}: {
  params: Promise<{ codeHash: string }>;
}) {
  const { codeHash } = await params;
  return <QrRedeemClient codeHash={codeHash} />;
}
