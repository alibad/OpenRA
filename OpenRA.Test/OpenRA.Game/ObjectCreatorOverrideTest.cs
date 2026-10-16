using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using OpenRA.FileSystem;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public class ObjectCreatorOverrideTest
	{
		sealed class ManifestPackage : IReadOnlyPackage
		{
			public string Name => "object-creator-test";
			public IEnumerable<string> Contents => ["mod.yaml"];
			public bool Contains(string filename) => filename == "mod.yaml";
			public Stream GetStream(string filename) => Contains(filename)
				? new MemoryStream(Encoding.UTF8.GetBytes("Metadata:\n\tTitle: Test\nFileSystem: DefaultFileSystem\nLoadScreen: NullLoadScreen\n")) : null;
			public IReadOnlyPackage OpenPackage(string filename, OpenRA.FileSystem.FileSystem context) => null;
			public void Dispose() { }
		}

		public sealed class HostImplementation { }

		[Test]
		public void ExplicitBindingReplacesACachedFactoryTypeAndLeavesOtherNamesAlone()
		{
			using var package = new ManifestPackage();
			using var creator = new ObjectCreator(new Manifest("test", package), new InstalledMods([], []));
			Assert.That(creator.CreateObject<object>("TypeDictionary"), Is.TypeOf<TypeDictionary>());
			creator.SetTypeOverride("TypeDictionary", typeof(HostImplementation));
			Assert.That(creator.FindType("TypeDictionary"), Is.EqualTo(typeof(HostImplementation)));
			Assert.That(creator.CreateObject<object>("TypeDictionary"), Is.TypeOf<HostImplementation>());
			Assert.That(creator.FindType("ActorInfo"), Is.EqualTo(typeof(ActorInfo)));
		}

		[Test]
		public void InvalidBindingsAreRejected()
		{
			using var package = new ManifestPackage();
			using var creator = new ObjectCreator(new Manifest("test", package), new InstalledMods([], []));
			Assert.Throws<ArgumentException>(() => creator.SetTypeOverride("", typeof(HostImplementation)));
			Assert.Throws<ArgumentNullException>(() => creator.SetTypeOverride("TraitInfo", null));
		}
	}
}
