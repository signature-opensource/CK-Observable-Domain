import { serialize, deserialize, ISerializeOptions, IDeserializeOptions } from '@local/ck-gen';
import { testWithIdempotence } from './util';
import { inspect } from "util";
import assert from 'assert';

if( process.env.VSCODE_INSPECTOR_OPTIONS ) jest.setTimeout(30 * 60 * 1000 ); // 30 minutes

describe('serialize() and deserialize()', function () {
    it('should work with embedded graphs', function () {

        const a = { v: 1, an: <any>[] };
        a.an.push(a);
        const s = '{"N":2,"G":' + serialize(a) + '}';
        const d = deserialize(s);

        expect( d.N ).toBe( 2 );
        expect( d.G.v ).toBe( 1 );
        expect( d.G.an[0] ).toBe( d.G );
    });

    it('should work with null references', function () {

        const a = { v: 1, n: null, an: [null, 2, null] };

        testWithIdempotence(a, undefined, d => {
            expect( d.n ).toBe( null );
            expect( d.an[0] ).toBe( null );
            expect( d.an[1] ).toBe( 2 );
            expect( d.an[2] ).toBe( null );
            expect( d.v ).toBe( 1 );
        });

    });

    it('should work with root array', function () {

        const a = [{ "v": 1 }, "A"];
        a.push(a[0]);
        testWithIdempotence( a );
    });

    it('should work with self reference', function () {

        const a = { v: 1 };
        (<any>a).rA = a;

        testWithIdempotence(a,undefined, d => {
            expect( d.rA ).toBe( d );
            expect( d.v ).toBe( 1 );
        });

    });

    it('should work with array reference', function () {

        const a = { arr: <any>["Test"] };
        a.arr.push(a);

        testWithIdempotence(a, undefined, d => {
            expect( d.arr.length ).toBe( 2 );
            expect( d.arr[0] ).toBe( 'Test' );
            expect( d.arr[1] ).toBe( d );
        });

    });

    it('should work with array reference and \'-\' prefix', function () {

        const a = { arr: <any>["Test"] };
        a.arr.push(a);

        testWithIdempotence(a, { prefix: "-" }, d => {
            expect( d.arr.length ).toBe( 2 );
            expect( d.arr[0] ).toBe( 'Test' );
            expect( d.arr[1] ).toBe( d );
        });

    });

    it('should work with multiple array references', function () {
        const a = [<any>[], []];
        a[0].push(a, a[0], a[1]);
        a[1].push(a, a[0], a[1]);

        testWithIdempotence(a, undefined, d => {
            expect(d.length ).toBe( 2);
            expect(d[0][0] ).toBe( d);
            expect(d[1][0] ).toBe( d);
            expect(d[1][1] ).toBe( d[0]);
            expect(d[1][2] ).toBe( d[1]);
        });
    });

    it('should work with empty prefix', function () {
        // a is graph with awful cycles in it.
        const a = <any>{ v: 1 }
        const b = { v: 2, rA: a };
        const c = { v: 3, rB: b, arr: [a, b] };
        a.rB = b;
        a.rC = c;

        testWithIdempotence(a, { prefix: "" });
    });

    it('should work with external objects (1)', function () {
        const ext = this;
        const a = [ext];

        const options = {
            substitor: r => {
                if (r === ext) return { EXT: 1 };
                return r;
            },
            activator: v => {
                if (v.EXT === 1) return ext;
                assert.fail("Activator is called only on substituted values.");
            }
        };

        testWithIdempotence(a, options, d => {
            expect(d[0] ).toBe( ext);
        });
    });

    it('should work with external objects (2)', function () {
        // a is graph with awful cycles in it.
        // and with references to srv1 and srv2.
        const srv1 = { host: this, addr: "an ip address" };
        const srv2 = { host: this, addr: "another ip address", master: srv1 };
        const a = <any>{ v: 1, server: srv1 }
        const b = { v: 2, rA: a, srv: [srv1, srv2] };
        const c = { v: 3, rB: b };
        const d = { v: 4, rC: c, all: [a, b, c], host: srv2 };
        a.rB = b;
        a.rC = c;
        a.rD = d;

        const options = {
            prefix: "",
            substitor: r => {
                if (r === srv1) return { SERVER: 1 };
                if (r === srv2) return { SERVER: 2 };
                return r;
            },
            activator: v => {
                if (v.SERVER === 1) return srv1;
                if (v.SERVER === 2) return srv2;
                assert.fail("Activator is called only on substituted values.");
            }
        };
        testWithIdempotence(a, options, d => {

        })
    });

    it('should handle conflicting prefixes', function () {
        // Conflicting prefix tests. 
        const conflictDemo0i = { "~$£€>": "idx" };
        const conflictDemo0r = { "~$£€>": null };
        const conflictDemo0t = { "~$£€þ": {} };
        const conflictDemo0Ai = [{ "~$£€>": "idx" }];
        const conflictDemo0Ar = [{ "~$£€>": null }];
        const conflictDemo0At = [{ "~$£€þ": {} }];
        const conflictDemo1 = { i: 1, Y: [{ "~$£€°": 9 }] };
        const conflictDemo2 = { i: 2, Y: [{ ">": 9 }] };
        const conflictDemo3 = { i: 3, Y: [{ "~þ": 9 }] };

        const checkConflict = function (o:any, options?:ISerializeOptions & IDeserializeOptions) {
            try {
                serialize(o, options);
                assert.fail('', '', "Conflict not detected:" + o);
            } catch (error) {
                // console.log("Conflict detected:", error);
            }
        }

        checkConflict(conflictDemo0i);
        checkConflict(conflictDemo0r);
        checkConflict(conflictDemo0t);
        checkConflict(conflictDemo0Ai);
        checkConflict(conflictDemo0Ar);
        checkConflict(conflictDemo0At);
        checkConflict(conflictDemo1);
        checkConflict(conflictDemo2, { prefix: "" });
        checkConflict(conflictDemo3, { prefix: "~" });
    });

    it('should work with Map objects', function () {

        const a = new Map();
        a.set('key', 'value')
            .set('otherKey', 987)
            .set(a, 'the map itself.')
            .set('theMap', a)
            .set('nullValue', null)
            .set(null, 'nullKey');

        testWithIdempotence(a, undefined, d => {
            expect( d.get('key') ).toBe( 'value' );
            expect( d.get(null) ).toBe( 'nullKey' );
            expect( d.get('nullValue') ).toBe( null );
        })
    });

    it('should work with Set objects', function () {
        const a = new Set();
        a.add(1)
            .add(a)
            .add(2);

        testWithIdempotence(a, { prefix: "" }, d => {
            expect( d.has(1) ).toBe( true );
            expect( d.has(d) ).toBe( true );
            expect( d.has(2) ).toBe( true );
        });
    });

    it('should work with array ref', function () {
        const e = {
            "N": 2,
            "E": [
                ["M", 37, "OREO", { "°": 53, "ProductId": "OREO", "ProductCode": "7622210021502", "ProductLabel": "Paquet d'Oreo", "ExtraData": null }],
                ["P", "Product", 50],
                ["C", 42, 50, { ">": 53 }]
            ]
        }

        const e2 = deserialize(e, { prefix: '' });

        console.log(inspect(e2, true, null, true));

        expect(e2.N ).toBe( 2 );
        expect(e2.E.length ).toBe( 3 );
        expect(e2.E[0].length ).toBe( 4 );
        expect(e2.E[0][3]).not.toBeUndefined;
        expect(e2.E[0][3]['°']).toBeUndefined;
        expect(e2.E[2].length ).toBe( 4 );
        expect(e2.E[2][3]).not.toBeUndefined;
        expect(e2.E[2][3]['>']).toBeUndefined;
        expect(e2.E[2][3]).toBe(e2.E[0][3]);
    });
});