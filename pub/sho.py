import clr
clr.AddReference('System.Windows.Forms')
clr.AddReferenceToFileAndPath('c:\\prgs\\dev\\Sho 2.0 for .NET 4\\bin\\EmbeddedSho.dll')
from ShoNS.Hosting import *

es = EmbeddedSho('c:\\prgs\\dev\\Sho 2.0 for .NET 4')

es.ExecutePython("a = rand(10,10)");
es.ExecutePython("foo = a[0,0]");
es.ExecutePython("plot([1,2,3,4,5])");