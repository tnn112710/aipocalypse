#!/usr/bin/env bash
# Usage: run.sh <file> <entry> [json args]
P="$(cygpath -w "$PWD")"
unity command script run --file "$1" --entry "$2" ${3:+--args "$3"} --project-path "$P" --json 2>&1 | node -e 'let s="";process.stdin.on("data",d=>s+=d).on("end",()=>{try{const j=JSON.parse(s);const r=j.data?.result;console.log(r?.success===false?(r.error+"\n"+r.errorDetails+"\n"+JSON.stringify(r.diagnostics)):(typeof r?.result==="string"?r.result:JSON.stringify(r?.result)))}catch(e){console.log(s.slice(0,800))}})'
