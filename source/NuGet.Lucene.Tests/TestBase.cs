using System;
using System.IO;
using System.Linq;
using Lucene.Net.Linq;
using Lucene.Net.Linq.Abstractions;
using Lucene.Net.Store;
using Moq;
using NUnit.Framework;
using Version = Lucene.Net.Util.Version;

namespace NuGet.Lucene.Tests
{
    public abstract class TestBase
    {
        protected Mock<ILucenePackageRepository> loader;
        protected Mock<IPackagePathResolver> packagePathResolver;
        protected Mock<IFileSystem> fileSystem;
        protected LuceneDataProvider provider;
        protected IQueryable<LucenePackage> datasource;
        protected IIndexWriter indexWriter;

        [SetUp]
        public void TestBaseSetUp()
        {
            packagePathResolver = new Mock<IPackagePathResolver>();
            loader = new Mock<ILucenePackageRepository>(MockBehavior.Strict);
            fileSystem = new Mock<IFileSystem>();

            packagePathResolver.Setup(p => p.GetPackageDirectory(It.IsAny<IPackage>())).Returns("package-dir");
            packagePathResolver.Setup(p => p.GetPackageFileName(It.IsAny<IPackage>())).Returns((Func<IPackage, string>)(pkg => pkg.Id));

            var dir = new RAMDirectory();

            provider = new LuceneDataProvider(dir, Version.LUCENE_30);
            indexWriter = provider.IndexWriter;

            datasource = provider.AsQueryable(() => new LucenePackage(fileSystem.Object));
        }

        protected LucenePackage MakeSamplePackage(string id, string version)
        {
            var p = new LucenePackage(path => new MemoryStream())
                        {
                            Id = id,
                            Version = version != null ? new StrictSemanticVersion(version) : null,
                            DownloadCount = -1,
                            VersionDownloadCount = -1
                        };

            if (p.Id != null && version != null)
            {
                p.Path = Path.Combine(packagePathResolver.Object.GetPackageDirectory(p),
                                      packagePathResolver.Object.GetPackageFileName(p));
            }

            return p;
        }

        protected void InsertPackage(string id, string version)
        {
            var p = MakeSamplePackage(id, version);

            p.DownloadCount = 0;
            p.VersionDownloadCount = 0;

            InsertPackage(p);
        }

        protected void InsertPackage(LucenePackage p)
        {
            p.Path = Path.Combine(packagePathResolver.Object.GetPackageDirectory(p),
                                  packagePathResolver.Object.GetPackageFileName(p));

            using (var s = provider.OpenSession(() => new LucenePackage(fileSystem.Object)))
            {
                s.Add(p);
            }
        }
    }
}