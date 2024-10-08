using CK.AspNet.Auth;
using CK.Auth;
using CK.Core;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.ServerSample.App;

public class FakeWebFrontAuthLoginService : IWebFrontAuthLoginService, IUserInfoProvider
{
    public bool HasBasicLogin => false;

    public IReadOnlyList<string> Providers => Array.Empty<string>();

    public Task<UserLoginResult> BasicLoginAsync( HttpContext ctx, IActivityMonitor monitor, string userName, string password, bool actualLogin = true )
        => Task.FromResult( new UserLoginResult( null, 1, "Login not supported!", false ) );

    public object CreatePayload( HttpContext ctx, IActivityMonitor monitor, string scheme )
    {
        throw new NotSupportedException();
    }

    public Task<UserLoginResult> LoginAsync( HttpContext ctx, IActivityMonitor monitor, string scheme, object payload, bool actualLogin = true )
        => Task.FromResult( new UserLoginResult( null, 1, "Login not supported!", false ) );

    public Task<IAuthenticationInfo> RefreshAuthenticationInfoAsync( HttpContext ctx, IActivityMonitor monitor, IAuthenticationInfo current, DateTime newExpires )
        => throw new NotSupportedException();

    ValueTask<IUserInfo> IUserInfoProvider.GetUserInfoAsync( IActivityMonitor monitor, int userId )
    {
        throw new NotImplementedException();
    }
}
