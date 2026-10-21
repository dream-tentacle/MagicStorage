const fs=require('node:fs');
const path=require('node:path');
const sharp=require(path.join(process.argv[2],'sharp'));
async function main(){
  const stem=path.join(__dirname,'StorageCore-v21');
  await sharp(fs.readFileSync(stem+'.svg'),{density:192})
    .resize(512,512).png().toFile(stem+'.png');
  console.log(stem+'.png');
}
main().catch(error=>{console.error(error.message);process.exitCode=1;});
