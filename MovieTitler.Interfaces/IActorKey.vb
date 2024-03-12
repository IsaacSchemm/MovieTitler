''' <summary>
''' A representation of the public key component of Crowmask's signing key.
''' </summary>
Public Interface IActorKey
    ''' <summary>
    ''' The public key, in PEM format.
    ''' </summary>
    ReadOnly Property Pem As String
End Interface
