import { foo } from "@signature/generated";
import {  DateTime } from "luxon";


export function foo2() {
    console.log("hello from dependent");
}
foo2();
foo();