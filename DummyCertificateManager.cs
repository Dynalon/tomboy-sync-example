using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace tomboysyncexample
{
	public class DummyCertificateManager : ICertificatePolicy
	{
        public bool CheckValidationResult (ServicePoint sp,
                                           X509Certificate certificate,
                                           WebRequest request,
                                           int error)

        {
                return true;
        }
	}
}

