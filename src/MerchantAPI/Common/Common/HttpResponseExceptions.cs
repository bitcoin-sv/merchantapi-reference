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

    public HttpResponseException(HttpStatusCode statusCode, string message, long executionTime) : base(message)
    {
      Status = (int)statusCode;
      ExecutionTime = executionTime;
    }

    public int Status { get; private set; }

    public object Value { get; set; }

    public long ExecutionTime { get; private set; }
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

    public BadRequestException(string message, long executionTime) : base(HttpStatusCode.BadRequest, message, executionTime) { }
  }

  public class NotFoundException : HttpResponseException
  {
    public NotFoundException(string message) : base(HttpStatusCode.NotFound, message) { }

    public NotFoundException(string message, Exception ex) : base(HttpStatusCode.NotFound, message, ex) { }

    public NotFoundException(string message, long executionTime) : base(HttpStatusCode.NotFound, message, executionTime) { }
  }

  /// <summary>
  /// Message of this exception can be shared by the user without exposing security sensitive information
  /// </summary>
  public class ExceptionWithSafeErrorMessage : Exception
  {
    public ExceptionWithSafeErrorMessage(string message) : base(message) { }
    public ExceptionWithSafeErrorMessage(string message, Exception ex) : base(message, ex) { }
  }

  public class ServiceUnavailableException : HttpResponseException
  {
    public ServiceUnavailableException(string message) : base(HttpStatusCode.NotFound, message) { }

    public ServiceUnavailableException(string message, Exception ex) : base(HttpStatusCode.NotFound, message, ex) { }

    public ServiceUnavailableException(string message, long executionTime) : base(HttpStatusCode.NotFound, message, executionTime) { }
  }
}

