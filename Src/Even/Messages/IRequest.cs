using System;

namespace Even.Messages
{
    interface IRequest
    {
        Guid RequestID { get; }
    }
}
