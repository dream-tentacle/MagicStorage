const fs = require('fs');
const path = require('path');
const sharp = require('C:/Users/x1353/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const folder=process.argv[2]==='Directions' ? path.join(__dirname,'Directions') : __dirname;
const manifestName=process.argv[2]==='Directions' ? 'manifest.json' : 'outline-manifest.json';
const records = JSON.parse(fs.readFileSync(path.join(folder,manifestName),'utf8'));
async function raw(source) {
  return sharp(source).ensureAlpha().raw().toBuffer({resolveWithObject:true});
}
(async () => {
  const report=[];
  for (const item of records.filter(item=>item.outline_mode==='outside')) {
    const svg=fs.readFileSync(path.join(folder,item.svg),'utf8');
    const header=svg.match(/<svg\b[^>]*>/)[0];
    const defs=svg.match(/<defs\b[^>]*>[\s\S]*?<\/defs>/g).join('\n');
    const mask=svg.match(/<mask id="exterior-outside-mask"[\s\S]*?<\/mask>/)[0];
    const geometry=mask.match(/<rect\b[^>]*\/>\s*(<(?:path|use)\b[^>]*\/>)/)[1]
      .replace('fill="#000000"','fill="#ffffff"');
    const silhouette=Buffer.from(header+defs+geometry+'</svg>');
    const noOutline=Buffer.from(svg.replace(/<g id="outer-only-outline"[^>]*>[\s\S]*?<\/g>/,''));
    const [full,base,inside]=await Promise.all([
      raw(path.join(folder,item.png)),raw(noOutline),raw(silhouette)
    ]);
    if (full.info.width!==base.info.width || full.info.height!==base.info.height) throw new Error(item.name+': canvas mismatch');
    let pixels=0,different=0;
    for (let i=0;i<full.data.length;i+=4) {
      if (inside.data[i+3]!==255 || base.data[i+3]!==255) continue;
      pixels++;
      if (full.data[i]!==base.data[i] || full.data[i+1]!==base.data[i+1] || full.data[i+2]!==base.data[i+2] || full.data[i+3]!==base.data[i+3]) different++;
    }
    report.push({name:item.name,interior_pixels:pixels,changed_interior_pixels:different});
    if (different || !pixels) throw new Error(item.name+': outline changed '+different+' interior pixels');
  }
  fs.writeFileSync(path.join(folder,'outside-outline-validation.json'),JSON.stringify(report,null,2));
  console.log(`Verified ${report.length} rendered outlines: zero changed opaque interior pixels.`);
})().catch(err=>{console.error(err);process.exit(1)});
