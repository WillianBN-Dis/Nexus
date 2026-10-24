using Nexus.Core;
using Microsoft.Data.Sqlite;
using System.Text.Json;
var root=Path.Combine(Path.GetTempPath(),"nexus-wpf-test-"+Guid.NewGuid().ToString("N"));
int checks=0;
void Assert(bool condition,string label){if(!condition)throw new Exception("FAIL: "+label);checks++;}
void Reject(Action action,string label){try{action();}catch(Exception){checks++;return;}throw new Exception("FAIL (allowed): "+label);}
try
{
 var store=new NexusStore(root);
 store.Register("tester","temporary12345");store.Login("tester","temporary12345");
 var item=store.Create("Teste WPF");store.Rename(item.Id,"Renomeado");Assert(store.Get(item.Id).Name=="Renomeado","rename");
 Assert(Convert.ToInt64(store.Execute(item.Id,"SELECT 42 AS resultado").Table.Rows[0][0])==42,"SELECT");
 string table="clientes \" e; teste";
 store.CreateTable(item.Id,table,[new("nome completo","TEXT",true),new("idade","INTEGER"),new("dados","BLOB")]);
 store.InsertRecord(item.Id,table,new Dictionary<string,object?>{{"nome completo","Ana'); DROP TABLE clients;--"},{"idade",30L},{"dados",new byte[]{0,1,255}}});
 store.InsertRecord(item.Id,table,new Dictionary<string,object?>{{"nome completo","Bia"},{"idade",20L},{"dados",null}});
 var page=store.BrowsePage(item.Id,table,"idade");Assert(page.Rows.Count==2&&Equals(page.Rows[0].Values[1],"Bia"),"sort/quoted identifiers");
 var before=page.Rows[0];store.ChangeRecord(item.Id,table,before,new Dictionary<string,object?>{{"nome completo","Beatriz"},{"idade",21L}});
 Reject(()=>store.ChangeRecord(item.Id,table,before,new Dictionary<string,object?>{{"idade",22L}}),"stale edit");
 page=store.BrowsePage(item.Id,table,"idade");store.ChangeRecord(item.Id,table,page.Rows[0],delete:true);Assert(store.BrowsePage(item.Id,table).Rows.Count==1,"delete only selected row");
 store.AddColumn(item.Id,table,"email","TEXT");Assert(store.TableInfo(item.Id,table).Columns.Count==5,"add column");
 Reject(()=>store.InsertRecord(item.Id,table,new Dictionary<string,object?>{{"nome completo",null}}),"NOT NULL");
 store.Execute(item.Id,"CREATE TABLE pairs(a TEXT,b TEXT,PRIMARY KEY(a,b)) WITHOUT ROWID");store.InsertRecord(item.Id,"pairs",new Dictionary<string,object?>{{"a","x"},{"b","y"}});
 var pair=store.BrowsePage(item.Id,"pairs").Rows[0];store.ChangeRecord(item.Id,"pairs",pair,new Dictionary<string,object?>{{"b","z"}});Assert(Equals(store.BrowsePage(item.Id,"pairs").Rows[0].Values[1],"z"),"composite key");
 store.Execute(item.Id,"CREATE TABLE child(parent INTEGER REFERENCES "+NexusStore.Quote(table)+"(id))");Reject(()=>store.InsertRecord(item.Id,"child",new Dictionary<string,object?>{{"parent",999L}}),"foreign key");
 store.Execute(item.Id,"CREATE TABLE numbers(n INTEGER)");
 store.Execute(item.Id,"WITH RECURSIVE seq(n) AS (VALUES(1) UNION ALL SELECT n+1 FROM seq WHERE n<205) INSERT INTO numbers SELECT n FROM seq");
 Assert(store.BrowsePage(item.Id,"numbers","n",true).HasMore,"pagination first page");Assert(store.BrowsePage(item.Id,"numbers","n",true,200).Rows.Count==5,"pagination last page");
 Assert(!store.IsReadOnly(item.Id,"UPDATE numbers SET n=n+1"),"detect SQL write");Assert(store.IsReadOnly(item.Id,"SELECT ';' AS symbol; -- comment"),"semicolon in string/comment");
 foreach(var sql in new[]{"ATTACH DATABASE ':memory:' AS other","PRAGMA writable_schema=ON","BEGIN","SELECT 1; DELETE FROM numbers","SELECT load_extension('x')"})Reject(()=>store.Execute(item.Id,sql),"SQL boundary "+sql);
 Reject(()=>store.Execute(item.Id,"WITH RECURSIVE seq(n) AS (VALUES(1) UNION ALL SELECT n+1 FROM seq WHERE n<1000000000) INSERT INTO numbers SELECT n FROM seq"),"5s mutation timeout");
 Assert(store.BrowsePage(item.Id,"numbers","n",true,200).Rows.Count==5,"timeout rolled back");
 using(var cancel=new CancellationTokenSource(50))Reject(()=>store.Execute(item.Id,"WITH RECURSIVE seq(n) AS (VALUES(1) UNION ALL SELECT n+1 FROM seq WHERE n<1000000000) SELECT SUM(n) FROM seq",cancel.Token),"cancel query");
 var destination=Path.Combine(root,"disk");Directory.CreateDirectory(destination);var oldPath=item.Path;store.Move(item.Id,destination);
 Assert(!File.Exists(oldPath)&&File.Exists(store.Get(item.Id).Path),"move closed/verified db");Assert(store.Tables(item.Id).Contains(table),"moved data persisted");
 Reject(()=>store.Move(item.Id,destination),"move collision");
 var lockedDestination=Path.Combine(root,"locked-disk");Directory.CreateDirectory(lockedDestination);
 using(var locked=new FileStream(store.Get(item.Id).Path,FileMode.Open,FileAccess.Read,FileShare.None))Reject(()=>store.Move(item.Id,lockedDestination),"locked source protected");
 Assert(File.Exists(store.Get(item.Id).Path),"source preserved on failed move");
 store.DropTable(item.Id,"numbers");Assert(!store.Tables(item.Id).Contains("numbers"),"drop table");
 store.Register("other","temporary12345");store.Login("other","temporary12345");Assert(store.List().Count==0,"account isolation");Reject(()=>store.Get(item.Id),"cross-account lookup");
 Reject(()=>store.Login("tester","wrong"),"bad password");Reject(()=>store.List(),"failed login clears session");store.Login("tester","temporary12345");Assert(store.List().Count==1,"persistence");
 if(args.Length>0)
 {
  var legacyRoot=args[0];var legacy=new NexusStore(legacyRoot);legacy.Login("usuário.teste","Nexus-Teste-123!ç");var legacyItem=legacy.List().Single();
  Assert(legacyItem.Name=="Banco legado","Python scrypt/login/catalog compatibility");Assert(legacy.BrowsePage(legacyItem.Id,"clientes").Rows.Count==2,"Python database compatibility");
  legacy.Register("csharp","Csharp-Teste-123!");File.WriteAllText(Path.Combine(legacyRoot,"csharp-test.json"),JsonSerializer.Serialize(new{Id=legacyItem.Id}));
 }
 store.Logout();Reject(()=>store.List(),"logout");
 Console.WriteLine($"PASS: {checks} verificações, incluindo SQL, limites, CRUD, isolamento e arquivos.");
}
finally{SqliteConnection.ClearAllPools();if(Directory.Exists(root))Directory.Delete(root,true);}
