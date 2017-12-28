﻿using AOPLib.Core;
using AOPLib.FilterAttribute;
using AOPLib.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Text;
using System.Threading.Tasks;

namespace AOPLib
{
    internal class DynamicProxy<T> : RealProxy
        where T : MarshalByRefObject
    {
        private T _target;

        private IMethodCallMessage callMethod = null;

        private IMethodReturnMessage returnMethod = null;

        public DynamicProxy(T target) : base(typeof(T))
        {
            _target = target;
        }

        /// <summary>
        /// 執行方法
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public override IMessage Invoke(IMessage msg)
        {
            callMethod = msg as IMethodCallMessage;
            MethodInfo targetMethod = callMethod.MethodBase as MethodInfo;

            FilterInfo Attrs = new FilterInfo(_target, targetMethod);

            try
            {
                ExcuteingContext excuting = Excuting(Attrs.ExcuteFilters);
                if (excuting.Result != null)
                {
                    returnMethod = GetReturnMessage(excuting.Result,excuting.Args);
                }
                else
                {
                    InvokeMethod(targetMethod, excuting);
                    //封裝執行後上下文

                    ExcutedContext excuted = Excuted(Attrs.ExcuteFilters);
                    if (excuted.Result != null)
                    {
                        returnMethod = GetReturnMessage(excuted.Result, excuted.Args);
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionContext exception = OnException(Attrs.ExceptionFilters, ex);
                //是否要自行處理錯誤
                if (exception.Result != null)
                {
                    returnMethod = GetReturnMessage(exception.Result, exception.Args);
                }
                else
                {
                    returnMethod = new ReturnMessage(ex, callMethod);
                }
            }
            return returnMethod;
        }

        private void InvokeMethod(MethodInfo targetMethod, ExcuteingContext excuting)
        {
            object result = targetMethod.Invoke(_target, excuting.Args);
            returnMethod = GetReturnMessage(result, excuting.Args);
        }

        /// <summary>
        /// 執行Exception過濾器
        /// </summary>
        /// <param name="exceptionFilter"></param>
        /// <param name="ex"></param>
        /// <returns></returns>
        private ExceptionContext OnException(IList<IExceptionFilter> exceptionFilter, Exception ex)
        {
            ExceptionContext excptionContext = new ExceptionContext(callMethod)
            {
                Exception = ex
            };

            foreach (var exFiter in exceptionFilter)
            {
                exFiter.OnException(excptionContext);
                if (excptionContext.Result != null)
                    break;
            }

            return excptionContext;
        }

        private ExcutedContext Excuted(IList<IExcuteFilter> filters)
        {
            ExcutedContext excutedContext = new ExcutedContext(returnMethod);

            foreach (var filter in filters)
            {
                filter.OnExcuted(excutedContext);
                if (excutedContext.Result != null)
                    break;
            }

            return excutedContext;
        }

        private ExcuteingContext Excuting(IList<IExcuteFilter> filters)
        { 
            //封裝執行前上下文
            ExcuteingContext excuteContext = new ExcuteingContext(callMethod);

            foreach (var filter in filters)
            {
                filter.OnExcuting(excuteContext);
                if (excuteContext.Result != null)
                    break;
            }

            return excuteContext;
        }

        private ReturnMessage GetReturnMessage(object result,object[] args)
        {
            return new ReturnMessage(result,
                                     args,
                                     args.Length,
                                     callMethod.LogicalCallContext,
                                     callMethod);
        }
    }
}