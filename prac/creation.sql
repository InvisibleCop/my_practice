CREATE TABLE IF NOT EXISTS FlatDate
(
    DateID      INTEGER PRIMARY KEY,
    Day         INTEGER     NOT NULL,
    Month       INTEGER     NOT NULL,
    Year        INTEGER     NOT NULL,
    Weekday     INTEGER     NOT NULL,
    MonthName   VARCHAR(50) NOT NULL,
    WeekdayName VARCHAR(50) NOT NULL
);

CREATE TABLE IF NOT EXISTS InfoSource
(
    SourceID    INTEGER PRIMARY KEY,
    Name        VARCHAR(50) NOT NULL,
    Link        VARCHAR(50) NOT NULL,
    Description VARCHAR(256) NOT NULL
);

CREATE TABLE IF NOT EXISTS Coin
(
    NumericalID INTEGER PRIMARY KEY,
    ID          VARCHAR(50) UNIQUE,
    Symbol      VARCHAR(50) UNIQUE,
    Name        VARCHAR(50) UNIQUE
);

CREATE TABLE IF NOT EXISTS DailyData
(
    NumericalID     INTEGER PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
    SourceID        INTEGER NOT NULL,
    CoinID          INTEGER NOT NULL,
    DateID          INTEGER NOT NULL,
    MarketCapUSD    DECIMAL NOT NULL,
    PriceUSD        DECIMAL NOT NULL,
    TradedVolumeUSD DECIMAL NOT NULL,
    
    FOREIGN KEY (SourceID) REFERENCES InfoSource(SourceID),
    FOREIGN KEY (CoinID) REFERENCES Coin (NumericalID),
    FOREIGN KEY (DateID) REFERENCES FlatDate (DateID)
);

CREATE TABLE IF NOT EXISTS DailyDiff
(
    NumericalID     INTEGER PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
    SourceID        INTEGER NOT NULL,
    CoinID          INTEGER NOT NULL,
    DateID          INTEGER NOT NULL,
    MarketCapDiff    DECIMAL NOT NULL,
    Return        DECIMAL NOT NULL,
    TradedVolumeDiff DECIMAL NOT NULL,
    LogReturn DECIMAL NOT NULL,
    ReturnPercent DECIMAL NOT NULL,

    FOREIGN KEY (SourceID) REFERENCES InfoSource(SourceID),
    FOREIGN KEY (CoinID) REFERENCES Coin (NumericalID),
    FOREIGN KEY (DateID) REFERENCES FlatDate (DateID)
);

CREATE TABLE IF NOT EXISTS VolatilityInfo
(
    NumericalID     INTEGER PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
    SourceID        INTEGER NOT NULL,
    CoinID          INTEGER NOT NULL,
    BeginDateID          INTEGER NOT NULL,
    EndDateID          INTEGER NOT NULL,
    DailyStDev      DOUBLE PRECISION NOT NULL,

    FOREIGN KEY (SourceID) REFERENCES InfoSource(SourceID),
    FOREIGN KEY (CoinID) REFERENCES Coin (NumericalID),
    FOREIGN KEY (BeginDateID) REFERENCES FlatDate (DateID),
    FOREIGN KEY (EndDateID) REFERENCES FlatDate (DateID)
);

CREATE TABLE IF NOT EXISTS CorrelationInfo
(
    NumericalID     INTEGER PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
    SourceID        INTEGER NOT NULL,
    FirstCoinID          INTEGER NOT NULL,
    SecondCoinID          INTEGER NOT NULL,
    BeginDateID          INTEGER NOT NULL,
    EndDateID          INTEGER NOT NULL,
    PearsonCorrelationValue      DOUBLE PRECISION NOT NULL,

    FOREIGN KEY (SourceID) REFERENCES InfoSource(SourceID),
    FOREIGN KEY (FirstCoinID) REFERENCES Coin (NumericalID),
    FOREIGN KEY (SecondCoinID) REFERENCES Coin (NumericalID),
    FOREIGN KEY (BeginDateID) REFERENCES FlatDate (DateID),
    FOREIGN KEY (EndDateID) REFERENCES FlatDate (DateID)
);

