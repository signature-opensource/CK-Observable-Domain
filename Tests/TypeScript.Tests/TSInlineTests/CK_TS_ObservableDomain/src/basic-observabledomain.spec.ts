import { ObservableDomain } from '@local/ck-gen';

if( process.env.VSCODE_INSPECTOR_OPTIONS ) jest.setTimeout(30 * 60 * 1000 ); // 30 minutes

describe('ObservableDomain eyeball tests - ', () => {
    it('Test 1', () => {

        const initial = '{"N":0,"P":[],"O":[{"þ":[0,"A"]}],"R":[],"C":1}';
        const t2 = '{"N":1,"E":[[["N",0,""],["P","Name",0],["C",0,0,"Hello!"],["P","Speed",1],["C",0,1,0],["P","Position",2],["C",0,2,{"Lat":0,"Long":0}],["P","CurrentMechanic",3],["C",0,3,null]]]}';
        const t3 = '{"N":2,"E":[[["D",0]]]}';
        const t4 = '{"N":3,"E":[[["N",0,""],["P","String",4],["P","Int32",5],["P","UInt32",6],["P","Int64",7],["P","UInt64",8],["P","Int16",9],["P","UInt16",10],["P","Byte",11],["P","SByte",12],["P","DateTime",13],["P","TimeSpan",14],["P","DateTimeOffset",15],["P","Guid",16],["P","Double",17],["P","Single",18],["P","Char",19],["P","Boolean",20],["C",0,4,"MultiPropertyType"],["C",0,5,-42],["C",0,6,42],["C",0,7,-2752512],["C",0,8,2752512],["C",0,9,-3712],["C",0,10,3712],["C",0,11,255],["C",0,12,-128],["C",0,13,"05/09/2018 16:06:47"],["C",0,14,"3.02:01:59.9950000"],["C",0,15,"05/09/2018 16:06:47 +02:00"],["C",0,16,"4f5e996d-51e9-4b04-b572-5126b14a5eca"],["C",0,17,3.59783E-77],["C",0,18,3.89740016544238E-05],["C",0,19,"c"],["C",0,20,true],["C",0,2,{"Lat":11.11,"Long":22.22}],["C",0,4,"MultiPropertyType"],["C",0,5,-42],["C",0,6,42],["C",0,7,-2752512],["C",0,8,2752512],["C",0,9,-3712],["C",0,10,3712],["C",0,11,255],["C",0,12,-128],["C",0,13,"05/09/2018 16:06:47"],["C",0,14,"3.02:01:59.9950000"],["C",0,15,"05/09/2018 16:06:47 +02:00"],["C",0,16,"4f5e996d-51e9-4b04-b572-5126b14a5eca"],["C",0,17,3.59783E-77],["C",0,18,3.89740016544238E-05],["C",0,19,"c"],["C",0,20,true],["C",0,2,{"Lat":11.11,"Long":22.22}],["P","Enum",21],["C",0,21,0]]]}';
        const t5 = '{"N":4,"E":[[["C",0,4,"Pouf"],["C",0,5,-39],["C",0,6,45],["C",0,7,-2752509],["C",0,8,2752515],["C",0,9,-3709],["C",0,10,3715],["C",0,11,2],["C",0,12,3],["C",0,13,"08/09/2018 16:06:47"],["C",0,14,"3.05:01:59.9950000"],["C",0,15,"05/09/2018 16:09:47 +02:00"],["C",0,16,"b681ad83-a276-4a5c-a11a-4a22469b6a0d"],["C",0,17,3],["C",0,18,3.00003886222839],["C",0,19,"f"],["C",0,20,false],["C",0,21,3],["C",0,2,{"Lat":14.11,"Long":25.22}]]]}';
        const t6 = '{"N":5,"E":[[["C",0,4,"MultiPropertyType"],["C",0,5,-42],["C",0,6,42],["C",0,7,-2752512],["C",0,8,2752512],["C",0,9,-3712],["C",0,10,3712],["C",0,11,255],["C",0,12,-128],["C",0,13,"05/09/2018 16:06:47"],["C",0,14,"3.02:01:59.9950000"],["C",0,15,"05/09/2018 16:06:47 +02:00"],["C",0,16,"4f5e996d-51e9-4b04-b572-5126b14a5eca"],["C",0,17,3.59783E-77],["C",0,18,3.89740016544238E-05],["C",0,19,"c"],["C",0,20,true],["C",0,2,{"Lat":11.11,"Long":22.22}]]]}';
        const t7 = '{"N":6,"E":[[["D",0],["N",0,"A"],["I",0,0,"One"],["I",0,1,"Two"]]]}';
        const t8 = '{"N":7,"E":[[["S",0,0,"Three"]]]}';

        var o = new ObservableDomain();
        o.applyWatchEvent(JSON.parse(initial));
        //console.log("intial Simple graph:", Array.from(o.allObjects));
        o.applyWatchEvent(JSON.parse(t2));
        //console.log("Simple graph after t2:", Array.from(o.allObjects));
        o.applyWatchEvent(JSON.parse(t3));
        //console.log("Simple graph after t3:", Array.from(o.allObjects));
        o.applyWatchEvent(JSON.parse(t4));
        //console.log("Simple graph after t4:", Array.from(o.allObjects));
        o.applyWatchEvent(JSON.parse(t5));
        //console.log("Simple graph after t5:", Array.from(o.allObjects));
        o.applyWatchEvent(JSON.parse(t6));
        //console.log("Simple graph after t6:", Array.from(o.allObjects));
        o.applyWatchEvent(JSON.parse(t7));
        //console.log("Simple graph after t7:", Array.from(o.allObjects));
        o.applyWatchEvent(JSON.parse(t8));
        //console.log("Simple graph after t8:", Array.from(o.allObjects));

    });

    it('Test 2 (sample)', () => {

        const initial = '{"N":1,"P":["Employees","Cars","CompanyName","FirstName","LastName","Garage","CurrentMechanic","CurrentCar","ReplacementCar","Name","Speed","Position","Friend","Level"],"O":[{"þ":[0,"A"]},{"°":1,"CompanyName":"Boite","Employees":{"$i":1,"$C":[{"þ":[2,"A"]},{"°":3,"CurrentCar":{"°":4,"Name":"Renault n°2","Speed":0,"Position":{"Lat":0,"Long":0},"CurrentMechanic":{">":3}},"Level":0,"Garage":{">":1},"Friend":null,"FirstName":"Scott","LastName":"Guthrie"}]},"Cars":{"$i":2,"$C":[{"þ":[5,"A"]},{"°":6,"Name":"Renault n°0","Speed":0,"Position":{"Lat":0,"Long":0},"CurrentMechanic":null},{"°":7,"Name":"Renault n°1","Speed":0,"Position":{"Lat":0,"Long":0},"CurrentMechanic":null},{">":4},{"°":8,"Name":"Renault n°3","Speed":0,"Position":{"Lat":0,"Long":0},"CurrentMechanic":null},{"°":9,"Name":"Renault n°4","Speed":0,"Position":{"Lat":0,"Long":0},"CurrentMechanic":null},{"°":10,"Name":"Renault n°5","Speed":0,"Position":{"Lat":0,"Long":0},"CurrentMechanic":null},{"°":11,"Name":"Renault n°6","Speed":0,"Position":{"Lat":0,"Long":0},"CurrentMechanic":null},{"°":12,"Name":"Renault n°7","Speed":0,"Position":{"Lat":0,"Long":0},"CurrentMechanic":null},{"°":13,"Name":"Renault n°8","Speed":0,"Position":{"Lat":0,"Long":0},"CurrentMechanic":null},{"°":14,"Name":"Renault n°9","Speed":0,"Position":{"Lat":0,"Long":0},"CurrentMechanic":null}]},"ReplacementCar":{"$i":3,"$C":[{"þ":[15,"M"]},[{">":6},{">":7}],[{">":4},{">":8}]]}},{">":2},{">":5},{">":15},{">":6},{">":7},{">":4},{">":8},{">":9},{">":10},{">":11},{">":12},{">":13},{">":14},{"°":16,"Friend":null,"FirstName":"Paul","LastName":"Minc"},{">":3},{"°":17,"CompanyName":null,"Employees":{"$i":17,"$C":[{"þ":[18,"A"]},{"°":19,"Garage":{">":17},"Friend":null,"FirstName":"Julien","LastName":"Mathon"},{"°":20,"CurrentCar":null,"Level":0,"Garage":{">":17},"Friend":null,"FirstName":"Idriss","LastName":"Hippocrate"},{"°":21,"CurrentCar":null,"Level":0,"Garage":{">":17},"Friend":null,"FirstName":"Cedric","LastName":"Legendre"},{"°":22,"CurrentCar":null,"Level":0,"Garage":{">":17},"Friend":null,"FirstName":"Benjamin","LastName":"Crosnier"},{"°":23,"CurrentCar":null,"Level":0,"Garage":{">":17},"Friend":null,"FirstName":"Alexandre","LastName":"Da Silva"},{"°":24,"CurrentCar":null,"Level":0,"Garage":{">":17},"Friend":null,"FirstName":"Olivier","LastName":"Spinelli"}]},"Cars":{"$i":18,"$C":[{"þ":[25,"A"]},{"°":26,"Name":"Volvo n°0","Speed":0,"Position":{"Lat":0,"Long":0},"CurrentMechanic":null},{"°":27,"Name":"Volvo n°1","Speed":0,"Position":{"Lat":0,"Long":0},"CurrentMechanic":null},{"°":28,"Name":"Volvo n°2","Speed":0,"Position":{"Lat":0,"Long":0},"CurrentMechanic":null},{"°":29,"Name":"Volvo n°3","Speed":0,"Position":{"Lat":0,"Long":0},"CurrentMechanic":null},{"°":30,"Name":"Volvo n°4","Speed":0,"Position":{"Lat":0,"Long":0},"CurrentMechanic":null},{"°":31,"Name":"Volvo n°5","Speed":0,"Position":{"Lat":0,"Long":0},"CurrentMechanic":null},{"°":32,"Name":"Volvo n°6","Speed":0,"Position":{"Lat":0,"Long":0},"CurrentMechanic":null},{"°":33,"Name":"Volvo n°7","Speed":0,"Position":{"Lat":0,"Long":0},"CurrentMechanic":null},{"°":34,"Name":"Volvo n°8","Speed":0,"Position":{"Lat":0,"Long":0},"CurrentMechanic":null},{"°":35,"Name":"Volvo n°9","Speed":0,"Position":{"Lat":0,"Long":0},"CurrentMechanic":null}]},"ReplacementCar":{"$i":19,"$C":[{"þ":[36,"M"]}]}},{">":18},{">":25},{">":36},{">":19},{">":20},{">":21},{">":22},{">":23},{">":24},{">":26},{">":27},{">":28},{">":29},{">":30},{">":31},{">":32},{">":33},{">":34},{">":35}],"R":[]}';
        const t1 = '{"N":2,"E":[[["C",16,2,"Signature Code"]]]}';
        const t2 = '{"N":3,"E":[[["CL",18],["N",36,""],["I",17,6,{">":36}],["C",36,5,{">":16}],["C",36,3,"X"],["C",36,4,"Y"],["C",36,7,null],["C",36,13,0],["C",36,12,null]]]}';
        const t3 = '{"N":4,"E":[[["R",17,5],["D",25]]]}';
        const t4 = '{"N":5,"E":[[["R",17,5],["D",25],["K",3,{">":4}]]]}';

        var o = new ObservableDomain();
        o.applyWatchEvent(JSON.parse(initial));
        //console.log("intial Sample graph:", Array.from(o.allObjects));
        o.applyWatchEvent(JSON.parse(t1));
        o.applyWatchEvent(JSON.parse(t2));
        //console.log("Sample graph after t1 and t2:", Array.from(o.allObjects));
        o.applyWatchEvent(JSON.parse(t3));
        //console.log("Sample graph after t3:", Array.from(o.allObjects));
        o.applyWatchEvent(JSON.parse(t4));
        //console.log("Sample graph after t4:", Array.from(o.allObjects));

    });
});