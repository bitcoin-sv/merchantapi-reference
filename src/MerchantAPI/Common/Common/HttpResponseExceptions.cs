// Copyright (c) 2020 Bitcoin Association

using System;
using System.Net;

namespace MerchantAPI.Common
{
  public class HttpResponseException : Exception
  {
    public HttpResponseException(HttpStatusCode statusCode, string message) : base(message)
    {
      Status = (int)statusCode;
    }

    public HttpResponseException(HttpStatusCode statusCode, string message, Exception ex) : base(message, ex) 
    {
      Status = (int)statusCode;
    }
    public int Status { get; private set; }

    public object Value { get; set; }
  }

  public class DomainException : HttpResponseException
  {
    public DomainException(string message) : base(HttpStatusCode.InternalServerError, message) { }

    public DomainException(string message, Exception ex) : base(HttpStatusCode.InternalServerError, message, ex) { }
  }


  public class BadRequestException : HttpResponseException
  {
    public BadRequestException(string message) : base(HttpStatusCode.BadRequest, message) { }

    public BadRequestException(string message, Exception ex) : base(HttpStatusCode.BadRequest, message, ex) { }
  }

  public class NotFoundException : HttpResponseException
  {
    public NotFoundException(string message) : base(HttpStatusCode.NotFound, message) { }

    public NotFoundException(string message, Exception ex) : base(HttpStatusCode.NotFound, message, ex) { }
  }

  /// <summary>
  /// Message of this exception can be shared by the user without exposing security sensitive information
  /// </summary>
  public class ExceptionWithSafeErrorMessage : Exception
  {
    public ExceptionWithSafeErrorMessage(string message) : base(message) { }
    public ExceptionWithSafeErrorMessage(string message, Exception ex) : base(message, ex) { }
  }
}

