import {mkdir,cp,rm} from 'node:fs/promises';
await rm('dist',{recursive:true,force:true});await mkdir('dist',{recursive:true});
for(const path of ['index.html','style.css','app.js','core.js','map.js','data','UPSTREAM-LICENSE.txt'])await cp(path,`dist/${path}`,{recursive:true});
console.log('Static GitHub Pages site built in dist/');
