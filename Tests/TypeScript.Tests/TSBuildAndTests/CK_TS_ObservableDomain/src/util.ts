
import { ISerializeOptions, serialize, IDeserializeOptions, deserialize  } from '@local/ck-gen';

export function testWithIdempotence(o : any, options?: ISerializeOptions & IDeserializeOptions, test?: (o: any) => void ) {
    options ??= {};
    const s = serialize(o, options);
    // s is the serialized string.
    // We test here the deserialize( s: any ):
    const onlyJSON = JSON.parse(s);
    const dFromJSON = deserialize(onlyJSON, options);
    if (test) test(dFromJSON);
    const sBackFromJSON = serialize(dFromJSON, options);
    if (s !== sBackFromJSON) {
        throw new Error("Idempotence failed for JSON object:" + sBackFromJSON);
    }
    const d = deserialize(s, options);
    if (test) test(d);
    const s2 = serialize(d, options);
    if (s !== s2) {
        throw new Error("Idempotence failed:" + s2);
    }
    return true;
};
