using System.Text;
using CK.Observable.WebSocketWatcher;
using NUnit.Framework;

namespace CK.Ng.ObservableDomain.WebSocket.Tests;

[TestFixture]
public class ObservableDomainWatcherPushTests
{
    static string CreatePushMessage( int transactionNumber, string exportedEvents, int lastExportedTransactionNumber, string? domainExport )
    {
        var m = ObservableDomainWatcher.CreatePushMessage( "D", transactionNumber, exportedEvents, lastExportedTransactionNumber, domainExport );
        return Encoding.UTF8.GetString( m.Span );
    }

    [Test]
    public void a_transaction_with_a_domain_export_is_pushed_as_the_full_export()
    {
        const string export = """{"N":3,"C":0,"P":[],"O":[],"R":[],"S":[]}""";
        CreatePushMessage( 3, """["I",0,0,1]""", 2, export ).ShouldBe( """["D",{"N":3,"C":0,"P":[],"O":[],"R":[],"S":[]}]""" );
        CreatePushMessage( 1, "", 0, export ).ShouldBe( """["D",{"N":3,"C":0,"P":[],"O":[],"R":[],"S":[]}]""" );
    }

    [Test]
    public void a_transaction_without_a_domain_export_is_pushed_as_its_events()
    {
        CreatePushMessage( 5, """["I",0,0,1],["D",0]""", 4, null ).ShouldBe( """["D",{"N":5,"E":[[["I",0,0,1],["D",0]]],"L":4}]""" );
    }

    [Test]
    public void a_transaction_that_changed_nothing_is_pushed_with_empty_events()
    {
        CreatePushMessage( 5, "", 4, null ).ShouldBe( """["D",{"N":5,"E":[[]],"L":4}]""" );
    }

    [Test]
    public void the_initial_transaction_without_a_domain_export_is_pushed_with_no_events()
    {
        CreatePushMessage( 1, "", 0, null ).ShouldBe( """["D",{"N":1,"E":[],"L":0}]""" );
    }
}
