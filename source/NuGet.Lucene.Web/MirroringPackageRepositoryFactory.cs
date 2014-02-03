using System;
using System.Net;
using NuGet.Lucene.Web.Models;
using NuGet.Lucene.Web.Util;

namespace NuGet.Lucene.Web
{
    public static class MirroringPackageRepositoryFactory
    {
        private const string UserAgent = "NuGet.Lucene.Web";

        public static IMirroringPackageRepository Create(IPackageRepository localRepository, string remotePackageUrl, TimeSpan timeout)
        {
            if (string.IsNullOrWhiteSpace(remotePackageUrl))
            {
                return new NonMirroringPackageRepository(localRepository);
            }

            var remoteRepository = CreateDataServicePackageRepository(new HttpClient(new Uri(remotePackageUrl)), timeout);

            return new MirroringPackageRepository(localRepository, remoteRepository, new WebCache());
        }

        public static DataServicePackageRepository CreateDataServicePackageRepository(IHttpClient httpClient, TimeSpan timeout)
        {
            var userAgent = string.Format("{0}/{1} ({2})",
                                          UserAgent,
                                          typeof (MirroringPackageRepositoryFactory).Assembly.GetName().Version,
                                          Environment.OSVersion);

            var remoteRepository = new DataServicePackageRepository(httpClient);

            remoteRepository.SendingRequest += (s, e) =>
                {
                    e.Request.Timeout = (int) timeout.TotalMilliseconds;

                    ((HttpWebRequest) e.Request).UserAgent = userAgent;

                    e.Request.Headers.Add(RepositoryOperationNames.OperationHeaderName, RepositoryOperationNames.Mirror);
                };

            return remoteRepository;
        }
    }
}