import {test,expect} from '@playwright/test';
test('login displays local bootstrap guidance and backend errors',async({page})=>{
 await page.route('**/v1/auth/login',route=>route.fulfill({status:401,body:'{}',contentType:'application/json'}));
 await page.goto('/');await expect(page.getByRole('heading',{name:'Sign in to your workspace'})).toBeVisible();
 await page.getByLabel(/Username/).fill('invalid');await page.getByLabel(/Password/).fill('invalid-password');
 await page.getByRole('button',{name:'Sign in',exact:true}).click();await expect(page.getByRole('alert')).toContainText('Request failed: 401');
});
