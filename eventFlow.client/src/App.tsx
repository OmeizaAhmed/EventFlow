import AcceptInvitation from './pages/acceptInvitation'

export default function App() {
  if (window.location.pathname === '/invitations/accept') {
    return <AcceptInvitation invitationId={new URLSearchParams(window.location.search).get('id')} />
  }

  return (
    <div>
      <h1 className="text-3xl font-bold underline text-blue-600">
        EventFlow
      </h1>
      <p className="text-lg text-gray-700">
        Welcome to EventFlow!
      </p>
    </div>
  )
}
