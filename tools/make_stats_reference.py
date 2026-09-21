"""PTCalc.Core.Tests/stats_reference.json fikstürünü üretir (SciPy + statsmodels gerekir).

Çalıştırma (depo kökünden):  python tools/make_stats_reference.py
Çıktı: stats_reference.json (çalışma dizinine); dosyayı PTCalc.Core.Tests/ altına kopyalayın.
Üretildiği sürümler: Python 3.14, SciPy 1.17.1, NumPy 2.4.4, statsmodels 0.14.6.
StatisticsReferenceTests bu değerleri ANOVA/Tukey, studentized range, OLS ve kalibrasyon için karşılaştırır.
"""
import numpy as np, json
from scipy import stats
import statsmodels.api as sm
from statsmodels.stats.multicomp import pairwise_tukeyhsd
out = {}
# ---- ANOVA: 3 formülasyonun çözünme %'si, n farklı
g = [[78.2, 80.1, 79.5, 81.0, 77.9, 80.4],
     [85.3, 86.1, 84.7, 87.2, 85.9],
     [79.0, 82.5, 81.1, 80.3, 83.0, 81.7, 80.9]]
F, p = stats.f_oneway(*g)
lev_mean = stats.levene(*g, center='mean'); lev_med = stats.levene(*g, center='median')
allv = np.concatenate(g); labels = np.concatenate([[i]*len(x) for i,x in enumerate(g)])
tk = pairwise_tukeyhsd(allv, labels, alpha=0.05)
grand = allv.mean(); ssb = sum(len(x)*(np.mean(x)-grand)**2 for x in g); ssw = sum(((np.array(x)-np.mean(x))**2).sum() for x in g)
k=len(g); N=len(allv); dfb=k-1; dfw=N-k
out['anova'] = dict(groups=g, F=F, p=p, ssb=ssb, ssw=ssw, dfb=dfb, dfw=dfw,
    eta2=ssb/(ssb+ssw), omega2=(ssb-dfb*ssw/dfw)/(ssb+ssw+ssw/dfw),
    levene_mean=dict(F=lev_mean.statistic,p=lev_mean.pvalue), levene_median=dict(F=lev_med.statistic,p=lev_med.pvalue),
    tukey=[dict(a=int(r[0]),b=int(r[1]),diff=float(r[2]),p=float(r[3]),lo=float(r[4]),hi=float(r[5]),reject=bool(r[6])) for r in tk._results_table.data[1:]],
    q_crit_05=float(stats.studentized_range.ppf(0.95, k, dfw)))
# studentized range spot checks
out['q'] = dict(cdf=[[q,k,df,float(stats.studentized_range.cdf(q,k,df))] for q,k,df in [(3.5,3,10),(2.0,4,20),(4.5,5,30),(3.0,2,5),(5.0,6,120),(3.877,3,10),(1.0,3,3)]],
                ppf=[[k,df,float(stats.studentized_range.ppf(0.95,k,df))] for k,df in [(3,10),(4,20),(5,30),(2,15),(6,60)]])
# ---- Çoklu regresyon: tablet sertliği ~ basınç, bağlayıcı %
X1=[10,12,14,16,18,20,10,12,14,16,18,20]; X2=[2,2,2,2,2,2,4,4,4,4,4,4]
Y=[5.1,5.9,6.8,7.4,8.3,9.0,6.2,7.1,7.7,8.6,9.4,10.3]
X=sm.add_constant(np.column_stack([X1,X2])); m=sm.OLS(Y,X).fit()
new=np.array([[1,15,3]]); pr=m.get_prediction(new).summary_frame(alpha=0.05)
out['regression']=dict(X1=X1,X2=X2,Y=Y,coef=m.params.tolist(),se=m.bse.tolist(),t=m.tvalues.tolist(),p=m.pvalues.tolist(),
    ci=m.conf_int(0.05).tolist(),r2=m.rsquared,r2adj=m.rsquared_adj,F=m.fvalue,Fp=m.f_pvalue,
    ss_reg=m.ess,ss_res=m.ssr,ss_tot=m.centered_tss,df_reg=int(m.df_model),df_res=int(m.df_resid),rmse=float(np.sqrt(m.mse_resid)),
    dw=float(sm.stats.durbin_watson(m.resid)),resid=m.resid.tolist(),
    pred=dict(x=[15,3],mean=float(pr['mean'][0]),ci=[float(pr['mean_ci_lower'][0]),float(pr['mean_ci_upper'][0])],pi=[float(pr['obs_ci_lower'][0]),float(pr['obs_ci_upper'][0])]))
# ---- Kalibrasyon: 5 düzey x 3 tekrar
conc=[1,1,1,2,2,2,5,5,5,10,10,10,20,20,20]
resp=[0.102,0.098,0.105,0.201,0.197,0.204,0.495,0.503,0.499,0.990,1.004,0.996,1.985,2.010,1.996]
lr=stats.linregress(conc,resp); n=len(conc); x=np.array(conc); y=np.array(resp)
yhat=lr.intercept+lr.slope*x; syx=float(np.sqrt(((y-yhat)**2).sum()/(n-2)))
tc=stats.t.ppf(0.975,n-2)
back=(y-lr.intercept)/lr.slope
out['calibration']=dict(conc=conc,resp=resp,slope=lr.slope,intercept=lr.intercept,se_slope=lr.stderr,se_intercept=lr.intercept_stderr,
    r=lr.rvalue,r2=lr.rvalue**2,syx=syx,t_crit=tc,ci_slope=[lr.slope-tc*lr.stderr,lr.slope+tc*lr.stderr],
    ci_intercept=[lr.intercept-tc*lr.intercept_stderr,lr.intercept+tc*lr.intercept_stderr],
    lod=3.3*syx/lr.slope,loq=10*syx/lr.slope,back_calc=back.tolist(),accuracy_pct=(back/x*100).tolist(),
    resid_pct=((y-yhat)/yhat*100).tolist(),
    # bilinmeyen: yanıt 0.75 → ters tahmin ve SE (Miller & Miller)
    unknown=dict(y0=0.75, x0=float((0.75-lr.intercept)/lr.slope),
      sx0=float(syx/lr.slope*np.sqrt(1+1/n+((0.75-y.mean())**2)/(lr.slope**2*((x-x.mean())**2).sum())))),
    unknown_m3=dict(y0=0.75, m=3, sx0=float(syx/lr.slope*np.sqrt(1/3+1/n+((0.75-y.mean())**2)/(lr.slope**2*((x-x.mean())**2).sum())))))
json.dump(out,open('stats_reference.json','w'),indent=1)
print(json.dumps({k:(v if k!='anova' else {kk:vv for kk,vv in v.items() if kk!='groups'}) for k,v in out.items() if k in('anova','q')},indent=1))
print('reg coef',out['regression']['coef'],'F',out['regression']['F'],'pred',out['regression']['pred'])
print('cal',out['calibration']['slope'],out['calibration']['intercept'],out['calibration']['lod'],out['calibration']['loq'])
