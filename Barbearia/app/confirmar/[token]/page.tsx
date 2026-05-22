import ConfirmarClient from './ConfirmarClient'

export default async function ConfirmarPage({
  params,
}: {
  params: Promise<{ token: string }>
}) {
  const { token } = await params
  return <ConfirmarClient token={token} />
}
