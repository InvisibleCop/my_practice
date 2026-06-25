INSERT INTO Coin (NumericalID, ID, Symbol, Name) 
VALUES
    (1, 'bitcoin', 'btc', 'Bitcoin'),
    (2, 'ethereum', 'eth', 'Ethereum'),
    (3, 'tether', 'usdt', 'Tether'),
    (4, 'binancecoin', 'bnb', 'BNB'),
    (5, 'usd-coin', 'usdc', 'USDC'),
    (6, 'ripple', 'xrp', 'XRP'),
    (7, 'solana', 'sol', 'Solana')
ON CONFLICT (NumericalID) DO NOTHING;

INSERT INTO InfoSource (SourceID, Name, Link, Description)
VALUES
    (1, 'CoinGecko API', 'https://www.coingecko.com/',
     'CoinGecko is the world’s largest independent crypto data aggregator (uses REST API)')
ON CONFLICT (SourceID) DO NOTHING;