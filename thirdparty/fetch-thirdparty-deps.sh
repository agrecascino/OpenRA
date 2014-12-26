#!/bin/bash
if [ ! -f StyleCop.dll ]; then
	echo "Fetching StyleCop files from nuget"
	nuget install StyleCop.MSBuild -Version 4.7.49.0
	cp ./StyleCop.MSBuild.4.7.49.0/tools/StyleCop*.dll .
	rm -rf StyleCop.MSBuild.4.7.49.0
fi

if [ ! -f ICSharpCode.SharpZipLib.dll ]; then
	echo "Fetching ICSharpCode.SharpZipLib from nuget"
	nuget install SharpZipLib -Version 0.86.0
	cp ./SharpZipLib.0.86.0/lib/20/ICSharpCode.SharpZipLib.dll .
	rm -rf SharpZipLib.0.86.0
fi

if [ ! -f MaxMind.GeoIP2.dll ]; then
	echo "Fetching MaxMind.GeoIP2 from nuget"
	nuget install MaxMind.GeoIP2 -Version 2.1.0
	cp ./MaxMind.Db.1.0.0.0/lib/net40/MaxMind.Db.* .
	rm -rf MaxMind.Db.1.0.0.0
	cp ./MaxMind.GeoIP2.2.1.0.0/lib/net40/MaxMind.GeoIP2* .
	rm -rf MaxMind.GeoIP2.2.1.0.0
	cp ./Newtonsoft.Json.6.0.5/lib/net40/Newtonsoft.Json* .
	rm -rf Newtonsoft.Json.6.0.5
	cp ./RestSharp.105.0.0/lib/net4-client/RestSharp* .
	rm -rf RestSharp.105.0.0
fi